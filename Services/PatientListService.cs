using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;

namespace ClinicalDataExplorer.Services;

public sealed record SavedPatientList(string Id, string Name, int Count);

// Ownership is always derived on the server, never supplied by the browser.
public sealed class PatientListService
{
    private readonly AuditStore store;
    private readonly AuthenticationStateProvider authentication;
    private readonly ApplicationSettingsService settings;
    private readonly UserActivityService activity;
    private readonly string server;
    public PatientListService(AuditStore store, AuthenticationStateProvider authentication,
        ApplicationSettingsService settings, UserActivityService activity)
    {
        this.store = store; this.authentication = authentication; this.settings = settings; this.activity = activity;
        server = ServerKey(settings.Current.FhirBaseUrl);
        using var connection = store.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PatientLists (
                Id TEXT PRIMARY KEY, Owner TEXT NOT NULL, Server TEXT NOT NULL, Name TEXT NOT NULL,
                UNIQUE(Owner, Server, Name COLLATE NOCASE));
            CREATE TABLE IF NOT EXISTS PatientListMembers (
                ListId TEXT NOT NULL, PatientId TEXT NOT NULL, PRIMARY KEY(ListId, PatientId));
            """;
        command.ExecuteNonQuery();
    }
    private async Task<(string Owner, string Actor, string Server)> ContextAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(user.Identity.Name))
            throw new UnauthorizedAccessException("Sign in to use private patient lists.");
        var owner = user.FindFirst(ClaimTypes.PrimarySid)?.Value ?? user.Identity.Name.ToUpperInvariant();
        if (ServerKey(settings.Current.FhirBaseUrl) != server)
            throw new InvalidOperationException("The FHIR server changed. Reload the page before using patient lists.");
        return (owner, user.Identity.Name, server);
    }
    private static string ServerKey(string url) => new Uri(url.TrimEnd('/') + "/").AbsoluteUri;
    public Task<IReadOnlyList<SavedPatientList>> ReadAsync() => AuditedAsync("PatientListRead", "/patient-lists", ReadCoreAsync);
    private async Task<IReadOnlyList<SavedPatientList>> ReadCoreAsync()
    {
        var context = await ContextAsync();
        using var connection = store.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Name,(SELECT COUNT(*) FROM PatientListMembers WHERE ListId=Id) FROM PatientLists WHERE Owner=$owner AND Server=$server ORDER BY Name COLLATE NOCASE";
        command.Parameters.AddWithValue("$owner", context.Owner);
        command.Parameters.AddWithValue("$server", context.Server);
        using var reader = command.ExecuteReader();
        var result = new List<SavedPatientList>();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        return result;
    }
    public Task<IReadOnlyList<string>> MembersAsync(string id) => AuditedAsync("PatientListMembersRead", ListTarget(id), () => MembersCoreAsync(id));
    private async Task<IReadOnlyList<string>> MembersCoreAsync(string id)
    {
        var context = await ContextAsync();
        using var connection = store.Open();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", context.Owner);
        command.Parameters.AddWithValue("$server", context.Server);
        command.CommandText = "SELECT COUNT(*) FROM PatientLists WHERE Id=$id AND Owner=$owner AND Server=$server";
        if ((long)command.ExecuteScalar()! != 1) throw new UnauthorizedAccessException("This list is unavailable.");
        command.CommandText = "SELECT PatientId FROM PatientListMembers WHERE ListId=$id ORDER BY PatientId";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }
    public async Task<string> CreateAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        await ChangeAsync("PatientListCreate", id, name: name);
        return id;
    }
    public Task RenameAsync(string id, string name) => ChangeAsync("PatientListRename", id, name: name);
    public Task DeleteAsync(string id) => ChangeAsync("PatientListDelete", id);
    public Task AddAsync(string id, string patientId) => ChangeAsync("PatientListAdd", id, patientId);
    public Task RemoveAsync(string id, string patientId) => ChangeAsync("PatientListRemove", id, patientId);
    private static string ValidateName(string name) => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80
        ? throw new ArgumentException("Enter a list name of 1–80 characters.") : name.Trim();
    private static string ValidatePatient(string id) => Regex.IsMatch(id, @"\A[A-Za-z0-9\-.]{1,64}\z")
        ? id : throw new ArgumentException("Invalid patient ID.");
    private Task ChangeAsync(string action, string id, string patient = "", string name = "") =>
        AuditedAsync(action, ListTarget(id), async () =>
        {
            await ChangeCoreAsync(action, id, patient, name);
            return true;
        }, recordSuccess: false);
    private static string ListTarget(string id) => "/patient-lists/" + (Guid.TryParseExact(id, "N", out _) ? id : "invalid-id");
    private async Task<T> AuditedAsync<T>(string action, string target, Func<Task<T>> operation, bool recordSuccess = true)
    {
        await activity.RecordAsync(action, target, "Attempted");
        try
        {
            var result = await operation();
            if (recordSuccess) await activity.RecordAsync(action, target, "Succeeded", result switch
            {
                IReadOnlyList<SavedPatientList> lists => "Lists returned: " + lists.Count,
                IReadOnlyList<string> patients => "Patient references returned: " + string.Join(", ", patients.Select(id => "Patient/" + id)),
                _ => ""
            });
            return result;
        }
        catch (Exception ex)
        {
            // The operation's transaction is disposed before a failure is recorded.
            // Do not include exception messages, list names, or arbitrary submitted values.
            await activity.RecordAsync(action, target, ex is UnauthorizedAccessException ? "Denied" : "Failed", ex.GetType().Name);
            throw;
        }
    }
    private async Task ChangeCoreAsync(string action, string id, string patient, string name)
    {
        var context = await ContextAsync();
        if (action is "PatientListCreate" or "PatientListRename") name = ValidateName(name);
        if (action is "PatientListAdd" or "PatientListRemove") patient = ValidatePatient(patient);
        using var connection = store.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", context.Owner);
        command.Parameters.AddWithValue("$server", context.Server);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$patient", patient);
        if (action != "PatientListCreate")
        {
            command.CommandText = "SELECT COUNT(*) FROM PatientLists WHERE Id=$id AND Owner=$owner AND Server=$server";
            if ((long)command.ExecuteScalar()! != 1) throw new UnauthorizedAccessException("This list is unavailable.");
        }
        command.CommandText = action switch
        {
            "PatientListCreate" => "INSERT INTO PatientLists VALUES ($id,$owner,$server,$name)",
            "PatientListRename" => "UPDATE PatientLists SET Name=$name WHERE Id=$id",
            "PatientListDelete" => "DELETE FROM PatientListMembers WHERE ListId=$id; DELETE FROM PatientLists WHERE Id=$id",
            "PatientListAdd" => "INSERT OR IGNORE INTO PatientListMembers VALUES ($id,$patient)",
            "PatientListRemove" => "DELETE FROM PatientListMembers WHERE ListId=$id AND PatientId=$patient",
            _ => throw new InvalidOperationException()
        };
        try { command.ExecuteNonQuery(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { throw new InvalidOperationException("A list with that name already exists.", ex); }
        command.CommandText = "INSERT INTO AuditEvents (Utc,User,Session,Action,Target,Outcome,Details) VALUES ($utc,$actor,$session,$action,$target,'Succeeded',$details)";
        command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$actor", context.Actor);
        command.Parameters.AddWithValue("$session", activity.Session);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$target", "/patient-lists/" + id);
        command.Parameters.AddWithValue("$details", patient.Length == 0 ? "" : "Patient/" + patient);
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
