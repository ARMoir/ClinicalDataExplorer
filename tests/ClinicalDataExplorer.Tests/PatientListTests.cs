using System.Security.Claims;
using ClinicalDataExplorer.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class PatientListTests : IDisposable
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("patient-lists-tests-");
    private string Database => Path.Combine(root.FullName, "audit.sqlite");
    private PatientListService Service(FhirTestContext context, string? user = "DOMAIN\\alice")
    {
        var authentication = new IdentityProvider(user);
        var audit = new AuditStore(Database);
        return new(audit, authentication, context.Settings, new UserActivityService(audit, authentication, context.Settings));
    }
    [Fact]
    public async Task Lists_persist_are_private_and_membership_is_case_sensitive_and_idempotent()
    {
        using var context = new FhirTestContext("https://fhir.example/r4/");
        var alice = Service(context);
        var id = await alice.CreateAsync(" Follow-up ");
        await alice.AddAsync(id, "p1"); await alice.AddAsync(id, "p1"); await alice.AddAsync(id, "P1");
        var reopened = Service(context, "domain\\ALICE");
        Assert.Equal(2, Assert.Single(await reopened.ReadAsync()).Count);
        Assert.Equal(2, (await reopened.MembersAsync(id)).Count);
        var bob = Service(context, "DOMAIN\\bob");
        Assert.Empty(await bob.ReadAsync()); Assert.Empty(await bob.MembersAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bob.AddAsync(id, "p2"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bob.RemoveAsync(id, "p1"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bob.RenameAsync(id, "Hijacked"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bob.DeleteAsync(id));
        await reopened.RenameAsync(id, "Review");
        Assert.Equal("Review", Assert.Single(await reopened.ReadAsync()).Name);
        await reopened.RemoveAsync(id, "p1");
        Assert.Equal("P1", Assert.Single(await reopened.MembersAsync(id)));
        await reopened.DeleteAsync(id);
        Assert.Empty(await reopened.ReadAsync()); Assert.Empty(await reopened.MembersAsync(id));
        var audit = new AuditStore(Database).Read();
        Assert.Contains(audit, e => e.Action == "PatientListCreate");
        Assert.Contains(audit, e => e.Action == "PatientListRename");
        Assert.Contains(audit, e => e.Action == "PatientListRemove");
        Assert.Contains(audit, e => e.Action == "PatientListDelete");
        Assert.DoesNotContain(audit, e => e.Details.Contains("Follow-up") || e.Details.Contains("Review"));
    }
    [Fact]
    public async Task Anonymous_access_validation_and_server_isolation_are_enforced()
    {
        using var context = new FhirTestContext("https://fhir.example/r4/");
        var service = Service(context);
        var id = await service.CreateAsync("Review");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync("review"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(id, "../Patient/p1"));
        var anonymous = Service(context, null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.ReadAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.CreateAsync("Review"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.MembersAsync(id));
        var settings = context.Settings.Current;
        settings.FhirBaseUrl = "https://other.example/r4/";
        await context.Settings.SaveAsync(settings);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(id, "p1"));
        var otherServer = Service(context);
        Assert.Empty(await otherServer.ReadAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => otherServer.AddAsync(id, "p1"));
        await otherServer.CreateAsync("Review");
    }
    [Fact]
    public async Task Audit_failure_rolls_back_list_changes()
    {
        using var context = new FhirTestContext("https://fhir.example/r4/");
        var service = Service(context);
        var id = await service.CreateAsync("Review");
        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER RejectAudit BEFORE INSERT ON AuditEvents BEGIN SELECT RAISE(ABORT, 'Unavailable'); END;";
            command.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<SqliteException>(() => service.AddAsync(id, "p1"));
        Assert.Empty(await service.MembersAsync(id));
        await Assert.ThrowsAsync<SqliteException>(() => service.DeleteAsync(id));
        Assert.Single(await service.ReadAsync());
    }
    [Fact]
    public async Task Saved_patient_reads_current_details_and_latest_encounter_without_observation_requests()
    {
        using var handler = new ScriptedHandler(
            request => { Assert.Equal("/r4/Patient/p1", request.RequestUri!.AbsolutePath); return ScriptedHandler.Xml("<Patient xmlns='http://hl7.org/fhir'><id value='p1'/><name><family value='Current'/></name></Patient>"); },
            request => { Assert.Contains("_sort=-date", request.RequestUri!.Query); Assert.Contains("_count=1", request.RequestUri.Query); return ScriptedHandler.Xml(FhirCompatibilityTests.Bundle("<entry><resource><Encounter><id value='e1'/><period><start value='2026-01-02'/></period></Encounter></resource></entry>")); });
        using var context = new FhirTestContext("https://fhir.example/r4/", handler);
        var result = await context.Service.GetSavedPatientAsync("p1");
        Assert.Equal("Current", result.Patient.DisplayName); Assert.Equal("e1", result.Encounter!.Id);
        Assert.Equal(2, handler.Calls);
    }
    public void Dispose() => root.Delete(true);
    private sealed class IdentityProvider(string? name) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(name is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Test"))));
    }
}
