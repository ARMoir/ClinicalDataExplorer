using Microsoft.Data.Sqlite;

namespace ClinicalDataExplorer.Services;

public sealed record AuditEntry(long Id, string Utc, string User, string Session, string Action, string Target, string Outcome, string Details);

// Only parameterized inserts and reads are exposed. Database files belong outside wwwroot.
public sealed class AuditStore
{
    private readonly string connectionString;
    public AuditStore(IWebHostEnvironment environment) : this(Path.Combine(environment.ContentRootPath, "App_Data", "audit.sqlite")) { }
    public AuditStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 30 }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS AuditEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, Utc TEXT NOT NULL, User TEXT NOT NULL,
                Session TEXT NOT NULL, Action TEXT NOT NULL, Target TEXT NOT NULL,
                Outcome TEXT NOT NULL, Details TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_Audit_User ON AuditEvents(User, Id);
            CREATE INDEX IF NOT EXISTS IX_Audit_Utc ON AuditEvents(Utc);
            CREATE TRIGGER IF NOT EXISTS Audit_NoUpdate BEFORE UPDATE ON AuditEvents BEGIN SELECT RAISE(ABORT, 'Audit records cannot be updated'); END;
            CREATE TRIGGER IF NOT EXISTS Audit_NoDelete BEFORE DELETE ON AuditEvents BEGIN SELECT RAISE(ABORT, 'Audit records cannot be deleted'); END;
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA synchronous=FULL;";
        command.ExecuteNonQuery();
        return connection;
    }
    public void Append(string user, string session, string action, string target, string outcome, string details = "")
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO AuditEvents (Utc,User,Session,Action,Target,Outcome,Details) VALUES ($utc,$user,$session,$action,$target,$outcome,$details)";
        command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$user", user);
        command.Parameters.AddWithValue("$session", session);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$target", target);
        command.Parameters.AddWithValue("$outcome", outcome);
        command.Parameters.AddWithValue("$details", details);
        command.ExecuteNonQuery(); // Deliberately propagate failures; never silently lose server audit events.
    }
    public IReadOnlyList<AuditEntry> Read(string user = "", string action = "", long before = long.MaxValue)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,Utc,User,Session,Action,Target,Outcome,Details FROM AuditEvents WHERE Id < $before AND ($user='' OR instr(lower(User),lower($user))>0) AND ($action='' OR instr(lower(Action),lower($action))>0) ORDER BY Id DESC LIMIT 50";
        command.Parameters.AddWithValue("$before", before);
        command.Parameters.AddWithValue("$user", user);
        command.Parameters.AddWithValue("$action", action);
        using var reader = command.ExecuteReader();
        var entries = new List<AuditEntry>();
        while (reader.Read()) entries.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7)));
        return entries;
    }
}
