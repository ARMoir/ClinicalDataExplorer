using System.Security.Claims;
using ClinicalDataExplorer.Models;
using ClinicalDataExplorer.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class AuditTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("ClinicalDataExplorer-audit-tests-");
    private string Database => Path.Combine(root.FullName, "audit.sqlite");

    [Fact]
    public async Task Records_persist_with_concurrent_writes_and_cannot_be_updated_or_deleted()
    {
        var store = new AuditStore(Database);
        await Task.WhenAll(Enumerable.Range(0, 60).Select(i => Task.Run(() => store.Append("DOMAIN\\user", "session", "Read", "Patient/" + i, "Succeeded"))));
        var reopened = new AuditStore(Database);
        var first = reopened.Read();
        Assert.Equal(50, first.Count);
        var second = reopened.Read(before: first[^1].Id);
        Assert.Equal(10, second.Count);
        Assert.Equal(60, first.Concat(second).Select(e => e.Id).Distinct().Count());
        Assert.All(first, e => Assert.Equal(TimeSpan.Zero, DateTimeOffset.Parse(e.Utc).Offset));
        using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
        connection.Open();
        foreach (var sql in new[] { "UPDATE AuditEvents SET User='changed'", "DELETE FROM AuditEvents" })
        {
            using var command = connection.CreateCommand(); command.CommandText = sql;
            Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        }
        Assert.Empty(reopened.Read(user: "' OR 1=1 --"));
    }

    [Fact]
    public async Task Admin_defaults_and_revocation_are_enforced_and_denials_are_audited()
    {
        using var context = new FhirTestContext("https://fhir.example.test/r4/");
        var store = new AuditStore(Database);
        var activity = new UserActivityService(store, new TestAuthentication("DOMAIN\\reader"), context.Settings);
        Assert.True(await activity.IsAdministratorAsync());
        var settings = context.Settings.Current;
        settings.AllUsersAreAdministrators = false;
        settings.AdministratorUsers = "DOMAIN\\admin";
        await context.Settings.SaveAsync(settings);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => activity.ReadAsync("", "", long.MaxValue));
        Assert.Equal("Denied", Assert.Single(store.Read()).Outcome);
        Assert.False(UserActivityService.IsAdministrator(new ClaimsPrincipal(new ClaimsIdentity()), settings));
        var admin = new UserActivityService(store, new TestAuthentication("domain\\ADMIN"), context.Settings);
        Assert.True(await admin.IsAdministratorAsync());
        Assert.NotEmpty(await admin.ReadAsync("", "", long.MaxValue));
        Assert.Contains(store.Read(), e => e.Action == "AuditReview");
    }

    [Fact]
    public async Task Fhir_reads_record_actor_resource_ids_and_outcome_without_search_values_or_clinical_content()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(FhirCompatibilityTests.Bundle("<entry><resource><Patient><id value='p1'/><name><family value='SensitiveName'/></name></Patient></resource></entry>")));
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var store = new AuditStore(Database);
        var activity = new UserActivityService(store, new TestAuthentication("DOMAIN\\clinician"), context.Settings);
        var service = new FhirService(context, context.Settings, activity);
        await service.SearchPatientsByIdentifierAsync("secret-mrn");
        var entries = store.Read();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Outcome == "Attempted");
        Assert.Contains(entries, e => e.Outcome == "Succeeded" && e.Details.Contains("Patient/p1"));
        Assert.All(entries, e => { Assert.Equal("DOMAIN\\clinician", e.User); Assert.DoesNotContain("secret-mrn", e.Target); Assert.DoesNotContain("SensitiveName", e.Details); });
    }

    [Fact]
    public async Task Audit_write_failure_prevents_Fhir_network_access()
    {
        using var handler = new ScriptedHandler();
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var store = new AuditStore(Database);
        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER RejectInsert BEFORE INSERT ON AuditEvents BEGIN SELECT RAISE(ABORT, 'Unavailable'); END;";
            command.ExecuteNonQuery();
        }
        var activity = new UserActivityService(store, new TestAuthentication("DOMAIN\\user"), context.Settings);
        await Assert.ThrowsAsync<SqliteException>(() => new FhirService(context, context.Settings, activity).SearchPatientsByIdentifierAsync("test"));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("https://server/r4/Patient?identifier=secret&_getpages=token", "/r4/Patient?_getpages&identifier")]
    [InlineData("/reports/view?reportId=123", "/reports/view?reportId")]
    public void Targets_exclude_query_values(string input, string expected) => Assert.Equal(expected, UserActivityService.SafeTarget(input));

    [Theory]
    [InlineData("/reports/view?reportId=r1&identifier=secret", "/reports/view?identifier&reportId; DiagnosticReport/r1")]
    [InlineData("/record?reference=Observation%2Fo1", "/record?reference; Observation/o1")]
    [InlineData("/record?reference=https://secret.example/Patient/p1?token=secret", "/record?reference")]
    [InlineData("/reports/view?identifier=secret", "/reports/view?identifier")]
    [InlineData("/reports/view?reportId=r1&reportId=r2", "/reports/view?reportId")]
    public void Page_targets_preserve_only_validated_resource_references(string input, string expected) =>
        Assert.Equal(expected, UserActivityService.SafePageTarget(input));

    [Theory]
    [InlineData(401, "Denied")]
    [InlineData(403, "Denied")]
    [InlineData(500, "Failed")]
    public async Task Fhir_http_failures_record_status_without_response_bodies(int status, string outcome)
    {
        using var handler = new ScriptedHandler(_ => new HttpResponseMessage((System.Net.HttpStatusCode)status)
        { Content = new StringContent("sensitive response body") });
        using var context = new FhirTestContext("https://fhir.example/r4/", handler);
        var store = new AuditStore(Database);
        var activity = new UserActivityService(store, new TestAuthentication("DOMAIN\\user"), context.Settings);
        await Assert.ThrowsAsync<HttpRequestException>(() => new FhirService(context, context.Settings, activity).GetPatientXmlAsync("p1"));
        var failure = Assert.Single(store.Read(), e => e.Outcome == outcome);
        Assert.Contains("HTTP " + status, failure.Details);
        Assert.DoesNotContain("sensitive", failure.Details);
    }

    private sealed class TestAuthentication(string name) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Test"))));
    }
    public void Dispose() => root.Delete(true);
}
