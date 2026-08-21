using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;

namespace WorkCosts.Services;

public sealed class DatabaseService
{
    private readonly string _dbPath;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DatabaseService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkCosts",
            "workcosts.db"))
    {
    }

    public DatabaseService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var folder = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _dbPath = databasePath;
    }

    public string DatabasePath => _dbPath;

    public Task Ready => _ready.Task;

    public WorkCostsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkCostsDbContext>()
            .UseSqlite($"Data Source={_dbPath};Cache=Shared")
            .Options;
        return new WorkCostsDbContext(options);
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Keep EF/SQLite off the UI thread so migrate/seed cannot stall input.
            await Task.Run(async () =>
            {
                await EnsureMigratedDatabaseAsync().ConfigureAwait(false);
                await using var db = CreateContext();
                await DbInitializer.SeedAsync(db).ConfigureAwait(false);
            }).ConfigureAwait(false);

            _ready.TrySetResult();
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            throw;
        }
    }

    private async Task EnsureMigratedDatabaseAsync()
    {
        if (File.Exists(_dbPath) && !await HasMigrationsHistoryAsync().ConfigureAwait(false))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
            DeleteSidecar(_dbPath + "-journal");
            DeleteSidecar(_dbPath + "-wal");
            DeleteSidecar(_dbPath + "-shm");
        }

        await using var db = CreateContext();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        await RepairProductSchemaAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Applies Product.Source / ProductEquivalents if a DB predates that migration.
    /// </summary>
    private async Task RepairProductSchemaAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath};Cache=Shared");
        await connection.OpenAsync().ConfigureAwait(false);

        if (!await ColumnExistsAsync(connection, "Products", "Source").ConfigureAwait(false))
        {
            await ExecuteAsync(connection,
                "ALTER TABLE Products ADD COLUMN Source TEXT NOT NULL DEFAULT '';").ConfigureAwait(false);
        }

        if (!await TableExistsAsync(connection, "ProductEquivalents").ConfigureAwait(false))
        {
            await ExecuteAsync(connection, """
                CREATE TABLE ProductEquivalents (
                    ProductId TEXT NOT NULL,
                    EquivalentProductId TEXT NOT NULL,
                    CONSTRAINT PK_ProductEquivalents PRIMARY KEY (ProductId, EquivalentProductId),
                    CONSTRAINT CK_ProductEquivalent_NotSelf CHECK (ProductId <> EquivalentProductId),
                    CONSTRAINT FK_ProductEquivalents_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ProductEquivalents_Products_EquivalentProductId FOREIGN KEY (EquivalentProductId) REFERENCES Products (Id) ON DELETE CASCADE
                );
                """).ConfigureAwait(false);

            await ExecuteAsync(connection,
                "CREATE INDEX IX_ProductEquivalents_EquivalentProductId ON ProductEquivalents (EquivalentProductId);")
                .ConfigureAwait(false);
        }

        if (!await MigrationHistoryContainsAsync(connection, "20260817120000_AddProductSourceAndEquivalents")
            .ConfigureAwait(false))
        {
            await ExecuteAsync(connection,
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260817120000_AddProductSourceAndEquivalents', '9.0.8');")
                .ConfigureAwait(false);
        }

        if (!await ColumnExistsAsync(connection, "Products", "PricePoint").ConfigureAwait(false))
        {
            await ExecuteAsync(connection,
                "ALTER TABLE Products ADD COLUMN PricePoint TEXT NOT NULL DEFAULT '';").ConfigureAwait(false);
        }

        if (!await MigrationHistoryContainsAsync(connection, "20260817131100_AddProductPricePoint")
            .ConfigureAwait(false))
        {
            await ExecuteAsync(connection,
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260817131100_AddProductPricePoint', '9.0.8');")
                .ConfigureAwait(false);
        }

        if (!await TableExistsAsync(connection, "CachedWebPages").ConfigureAwait(false))
        {
            await ExecuteAsync(connection, """
                CREATE TABLE CachedWebPages (
                    Id TEXT NOT NULL CONSTRAINT PK_CachedWebPages PRIMARY KEY,
                    PageUrl TEXT NOT NULL,
                    Domain TEXT NOT NULL,
                    RelativePath TEXT NOT NULL,
                    ByteSize INTEGER NOT NULL,
                    CachedAtUtc TEXT NOT NULL
                );
                """).ConfigureAwait(false);
            await ExecuteAsync(connection,
                "CREATE UNIQUE INDEX IX_CachedWebPages_PageUrl ON CachedWebPages (PageUrl);")
                .ConfigureAwait(false);
            await ExecuteAsync(connection,
                "CREATE INDEX IX_CachedWebPages_Domain ON CachedWebPages (Domain);")
                .ConfigureAwait(false);
        }

        if (!await TableExistsAsync(connection, "CachedWebImages").ConfigureAwait(false))
        {
            await ExecuteAsync(connection, """
                CREATE TABLE CachedWebImages (
                    Id TEXT NOT NULL CONSTRAINT PK_CachedWebImages PRIMARY KEY,
                    PageUrl TEXT NOT NULL,
                    ImageUrl TEXT NOT NULL,
                    Domain TEXT NOT NULL,
                    RelativePath TEXT NOT NULL,
                    ContentType TEXT NOT NULL,
                    ByteSize INTEGER NOT NULL,
                    CachedAtUtc TEXT NOT NULL
                );
                """).ConfigureAwait(false);
            await ExecuteAsync(connection,
                "CREATE UNIQUE INDEX IX_CachedWebImages_PageUrl_ImageUrl ON CachedWebImages (PageUrl, ImageUrl);")
                .ConfigureAwait(false);
            await ExecuteAsync(connection,
                "CREATE INDEX IX_CachedWebImages_Domain ON CachedWebImages (Domain);")
                .ConfigureAwait(false);
        }

        if (!await MigrationHistoryContainsAsync(connection, "20260819000100_AddWebCacheLookup")
            .ConfigureAwait(false))
        {
            await ExecuteAsync(connection,
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260819000100_AddWebCacheLookup', '9.0.8');")
                .ConfigureAwait(false);
        }

        if (!await ColumnExistsAsync(connection, "Products", "ExtraYaml").ConfigureAwait(false))
        {
            await ExecuteAsync(connection,
                "ALTER TABLE Products ADD COLUMN ExtraYaml TEXT NOT NULL DEFAULT '';").ConfigureAwait(false);
        }

        if (!await MigrationHistoryContainsAsync(connection, "20260821120000_AddProductExtraYaml")
            .ConfigureAwait(false))
        {
            await ExecuteAsync(connection,
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260821120000_AddProductExtraYaml', '9.0.8');")
                .ConfigureAwait(false);
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT 1 FROM pragma_table_info('{table}') WHERE name = $column LIMIT 1;";
        command.Parameters.AddWithValue("$column", column);
        return await command.ExecuteScalarAsync().ConfigureAwait(false) is not null;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $table LIMIT 1;";
        command.Parameters.AddWithValue("$table", table);
        return await command.ExecuteScalarAsync().ConfigureAwait(false) is not null;
    }

    private static async Task<bool> MigrationHistoryContainsAsync(SqliteConnection connection, string migrationId)
    {
        if (!await TableExistsAsync(connection, "__EFMigrationsHistory").ConfigureAwait(false))
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", migrationId);
        return await command.ExecuteScalarAsync().ConfigureAwait(false) is not null;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static void DeleteSidecar(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<bool> HasMigrationsHistoryAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath};Cache=Shared");
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory' LIMIT 1;";
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return result is not null;
    }
}
