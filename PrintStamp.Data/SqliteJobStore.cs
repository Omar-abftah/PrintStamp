using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using PrintStamp.Core;

namespace PrintStamp.Data;

public sealed class SqliteJobStore : IJobStore, IAdminStore
{
    const int UniqueViolation = 2067;
    readonly string _cs;

    public SqliteJobStore(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        _cs = $"Data Source={dbPath}";
        using var c = new SqliteConnection(_cs);
        c.Open();
        c.Execute("""
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Entity (
                Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE, IsActive INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE IF NOT EXISTS PrintJob (
                Id INTEGER PRIMARY KEY, Code TEXT NOT NULL UNIQUE,
                IssuerId INTEGER NOT NULL REFERENCES Entity(Id),
                ReceiverId INTEGER NOT NULL REFERENCES Entity(Id),
                CreatedAt TEXT NOT NULL, PageCount INTEGER NOT NULL,
                StoredAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Setting (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Admin (
                Id INTEGER PRIMARY KEY, Username TEXT NOT NULL UNIQUE COLLATE NOCASE, PasswordHash TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS LookupLog (
                Id INTEGER PRIMARY KEY, At TEXT NOT NULL, Admin TEXT NOT NULL, Code TEXT NOT NULL, Found INTEGER NOT NULL);
            """);
        var cols = c.Query<string>("SELECT name FROM pragma_table_info('PrintJob')").ToList();
        if (!cols.Contains("StoredAt"))
        {
            c.Execute("ALTER TABLE PrintJob ADD COLUMN StoredAt TEXT");
            c.Execute("UPDATE PrintJob SET StoredAt = @now", new { now = NowUtcIso() });
        }
    }

    async Task<SqliteConnection> OpenAsync()
    {
        var c = new SqliteConnection(_cs);
        await c.OpenAsync();
        // foreign keys are per-connection in SQLite; two apps share this file, so wait on locks
        await c.ExecuteAsync("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;");
        return c;
    }

    static string Iso(DateTime d) => d.ToString("o", CultureInfo.InvariantCulture);
    static DateTime ParseIso(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    // ---------- entities ----------
    public async Task<IReadOnlyList<Entity>> GetEntitiesAsync(bool includeInactive = false)
    {
        await using var c = await OpenAsync();
        var sql = "SELECT Id, Name, IsActive FROM Entity" + (includeInactive ? "" : " WHERE IsActive = 1") + " ORDER BY Name";
        return (await c.QueryAsync<Entity>(sql)).AsList();
    }

    public async Task<int> AddEntityAsync(string name)
    {
        await using var c = await OpenAsync();
        try
        {
            return (int)await c.ExecuteScalarAsync<long>(
                "INSERT INTO Entity (Name) VALUES (@name); SELECT last_insert_rowid();", new { name = name.Trim() });
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == UniqueViolation)
        { throw new DuplicateEntityException(name); }
    }

    public async Task RenameEntityAsync(int id, string name)
    {
        await using var c = await OpenAsync();
        try { await c.ExecuteAsync("UPDATE Entity SET Name = @name WHERE Id = @id", new { id, name = name.Trim() }); }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == UniqueViolation)
        { throw new DuplicateEntityException(name); }
    }
    public async Task SetEntityActiveAsync(int id, bool active)
    {
        await using var c = await OpenAsync();
        await c.ExecuteAsync("UPDATE Entity SET IsActive = @a WHERE Id = @id", new { id, a = active ? 1 : 0 });
    }

    // ---------- jobs ----------
    public async Task<PrintJob> CreateJobAsync(int issuerId, int receiverId, DateTime createdAt, int pageCount)
    {
        await using var c = await OpenAsync();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var code = CodeGenerator.New();
            try
            {
                var id = await c.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO PrintJob (Code, IssuerId, ReceiverId, CreatedAt, PageCount, StoredAt)
                    VALUES (@code, @issuerId, @receiverId, @at, @pageCount, @storedAt);
                    SELECT last_insert_rowid();
                    """,
                    new { code, issuerId, receiverId, at = Iso(createdAt), pageCount, storedAt = NowUtcIso() });

                return new PrintJob { Id = id, Code = code, IssuerId = issuerId, ReceiverId = receiverId,
                                      CreatedAt = createdAt, PageCount = pageCount };
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == UniqueViolation)
            {
                // two jobs drew the same code: extremely unlikely, just draw again
            }
        }
        throw new InvalidOperationException("Could not generate a unique code.");
    }

    public async Task<JobDetails?> FindByCodeAsync(string code)
    {
        await using var c = await OpenAsync();
        var row = await c.QuerySingleOrDefaultAsync<(string Code, string Issuer, string Receiver, string At, int Pages)?>(
            """
            SELECT j.Code, i.Name AS Issuer, r.Name AS Receiver, j.CreatedAt AS At, j.PageCount AS Pages
            FROM PrintJob j
            JOIN Entity i ON i.Id = j.IssuerId
            JOIN Entity r ON r.Id = j.ReceiverId
            WHERE j.Code = @code
            """, new { code = CodeGenerator.Normalize(code) });

        return row is null ? null : new JobDetails
        {
            Code = row.Value.Code, IssuerName = row.Value.Issuer, ReceiverName = row.Value.Receiver,
            CreatedAt = ParseIso(row.Value.At), PageCount = row.Value.Pages
        };
    }
    
    static string NowUtcIso() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    // ---------- barcode position ----------
    public async Task<BarcodePos?> GetBarcodePosAsync()
    {
        await using var c = await OpenAsync();
        var x = await c.ExecuteScalarAsync<string?>("SELECT Value FROM Setting WHERE Key = 'BarcodeX'");
        var y = await c.ExecuteScalarAsync<string?>("SELECT Value FROM Setting WHERE Key = 'BarcodeY'");
        if (x is null || y is null) return null;
        return new BarcodePos(double.Parse(x, CultureInfo.InvariantCulture), double.Parse(y, CultureInfo.InvariantCulture));
    }

    public async Task SetBarcodePosAsync(BarcodePos pos)
    {
        await using var c = await OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        const string sql = "INSERT INTO Setting (Key, Value) VALUES (@k, @v) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
        await c.ExecuteAsync(sql, new { k = "BarcodeX", v = pos.X.ToString("R", CultureInfo.InvariantCulture) }, tx);
        await c.ExecuteAsync(sql, new { k = "BarcodeY", v = pos.Y.ToString("R", CultureInfo.InvariantCulture) }, tx);
        await tx.CommitAsync();
    }
    
    // ---------- admin ----------
    public async Task<bool> AnyAdminAsync()
    {
        await using var c = await OpenAsync();
        return await c.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Admin") > 0;
    }

    public async Task CreateAdminAsync(string username, string passwordHash)
    {
        await using var c = await OpenAsync();
        // the WHERE NOT EXISTS makes "first admin" atomic: a second attempt inserts nothing
        var rows = await c.ExecuteAsync(
            "INSERT INTO Admin (Username, PasswordHash) SELECT @u, @h WHERE NOT EXISTS (SELECT 1 FROM Admin)",
            new { u = username.Trim(), h = passwordHash });
        if (rows != 1) throw new InvalidOperationException("An admin already exists.");
    }

    public async Task<string?> GetPasswordHashAsync(string username)
    {
        await using var c = await OpenAsync();
        return await c.ExecuteScalarAsync<string?>("SELECT PasswordHash FROM Admin WHERE Username = @u", new { u = username.Trim() });
    }

    public async Task LogLookupAsync(string admin, string code, bool found)
    {
        await using var c = await OpenAsync();
        await c.ExecuteAsync("INSERT INTO LookupLog (At, Admin, Code, Found) VALUES (@at, @admin, @code, @found)",
            new { at = NowUtcIso(), admin, code, found = found ? 1 : 0 });
    }

    public async Task<IReadOnlyList<LookupLogEntry>> RecentLookupsAsync(int count)
    {
        await using var c = await OpenAsync();
        var rows = await c.QueryAsync<(string At, string Admin, string Code, int Found)>(
            "SELECT At, Admin, Code, Found FROM LookupLog ORDER BY Id DESC LIMIT @count", new { count });
        return rows.Select(r => new LookupLogEntry(ParseIso(r.At).ToLocalTime(), r.Admin, r.Code, r.Found == 1)).ToList();
    }

    public async Task<int> PurgeExpiredAsync(DateTime cutoffUtc)
    {
        var cutoff = Iso(cutoffUtc.ToUniversalTime());
        await using var c = await OpenAsync();

        await c.ExecuteAsync("PRAGMA secure_delete=ON;");   // overwrite deleted content instead of leaving it in free pages
        int jobs = await c.ExecuteAsync("DELETE FROM PrintJob WHERE StoredAt < @cutoff", new { cutoff });
        int logs = await c.ExecuteAsync("DELETE FROM LookupLog WHERE At < @cutoff", new { cutoff });

        if (jobs + logs > 0)
        {
            try
            {
                await c.ExecuteAsync("VACUUM;");
                await c.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE);");
            }
            catch (SqliteException) {}
        }
        return jobs;
    }
}