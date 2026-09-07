using Finort.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Finort.Services;

public class DatabaseMigrator
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(AppDbContext db, ILogger<DatabaseMigrator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void Migrate()
    {
        try
        {
            _logger.LogInformation("Applying database migrations...");
            _db.Database.Migrate();
            _logger.LogInformation("Migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Migrate failed, attempting recovery...");
            TryRecoverMigrationHistory(ex);
        }
    }

    private void TryRecoverMigrationHistory(Exception originalException)
    {
        try
        {
            _logger.LogInformation("Attempting to sync migration history...");

            if (_db.Database.GetDbConnection() is not SqliteConnection connection)
            {
                _logger.LogError("Recovery only supports SQLite");
                throw originalException;
            }

            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            var pendingMigrations = _db.Database.GetPendingMigrations().ToList();
            _logger.LogInformation("Pending migrations: {Pending}", string.Join(", ", pendingMigrations));

            foreach (var migration in pendingMigrations)
            {
                if (MigrationChangesAlreadyExist(connection, migration))
                {
                    _logger.LogInformation("Migration {Migration} already applied in backup, marking as applied", migration);
                    MarkMigrationAsApplied(connection, migration);
                }
                else
                {
                    _logger.LogInformation("Migration {Migration} needs to be applied", migration);
                    ApplyPendingMigrationDirectly(connection, migration);
                    MarkMigrationAsApplied(connection, migration);
                }
            }

            _logger.LogInformation("All migrations resolved after recovery");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration recovery failed");
            throw;
        }
    }

    private bool MigrationChangesAlreadyExist(SqliteConnection connection, string migrationId)
    {
        using var cmd = connection.CreateCommand();

        if (migrationId.Contains("ReembolsoCategoria"))
        {
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Lancamentos') WHERE name = 'ReembolsoCategoriaId'";
            return (long)cmd.ExecuteScalar()! > 0;
        }

        if (migrationId.Contains("SeedDadosCompletos") || migrationId.Contains("AdicionarLimiteConta"))
        {
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Contas') WHERE name = 'Limite'";
            return (long)cmd.ExecuteScalar()! > 0;
        }

        if (migrationId.Contains("AdicionarDataCompra") || migrationId.Contains("DataCompraSeedIndefinida"))
        {
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Lancamentos') WHERE name = 'DataCompra'";
            return (long)cmd.ExecuteScalar()! > 0;
        }

        _logger.LogWarning("Unknown migration {Migration}, assuming it needs to be applied", migrationId);
        return false;
    }

    private void ApplyPendingMigrationDirectly(SqliteConnection connection, string migrationId)
    {
        _logger.LogInformation("Applying migration {Migration} via direct SQL", migrationId);

        if (migrationId.Contains("ReembolsoCategoria"))
        {
            ExecuteSql(connection, "ALTER TABLE Lancamentos ADD COLUMN ReembolsoCategoriaId TEXT");
            ExecuteSql(connection, "ALTER TABLE Lancamentos ADD COLUMN ReembolsoSubcategoriaId TEXT");
            ExecuteSql(connection, "CREATE INDEX IF NOT EXISTS IX_Lancamentos_ReembolsoCategoriaId ON Lancamentos (ReembolsoCategoriaId)");
            ExecuteSql(connection, "CREATE INDEX IF NOT EXISTS IX_Lancamentos_ReembolsoSubcategoriaId ON Lancamentos (ReembolsoSubcategoriaId)");
        }
        else
        {
            throw new InvalidOperationException($"Cannot apply unknown migration: {migrationId}");
        }
    }

    private void ExecuteSql(SqliteConnection connection, string sql)
    {
        _logger.LogInformation("Executing: {Sql}", sql);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void MarkMigrationAsApplied(SqliteConnection connection, string migrationId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) 
            VALUES (@id, '9.0.0')";
        cmd.Parameters.AddWithValue("@id", migrationId);
        cmd.ExecuteNonQuery();
    }
}