using Finort.Data;
using Finort.Models.Configuration;
using Finort.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Finort.Tests;

public class DatabaseMigratorTests
{
    [Fact]
    public void Migrate_AppliesInitialCreate_OnFreshSqlite()
    {
        var file = Path.Combine(Path.GetTempPath(), $"cf-test-{Guid.NewGuid():N}.db");
        try
        {
            var config = new DatabaseConfig
            {
                Provider = "Sqlite",
                Sqlite = new DatabaseConnectionSettings { ConnectionString = $"Data Source={file}" }
            };

            var options = DbContextOptionsBuilderFactory.Build(config);
            using var db = new AppDbContext(options);

            var migrator = new DatabaseMigrator(db, NullLogger<DatabaseMigrator>.Instance);

            migrator.Migrate();

            Assert.Empty(db.Database.GetPendingMigrations());

            var migrationIds = new List<string>();
            using (var conn = new SqliteConnection($"Data Source={file}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    migrationIds.Add(reader.GetString(0));
                }
            }

            Assert.Contains(migrationIds, id =>
                id.EndsWith("_Initial", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(file)) File.Delete(file);
        }
    }
}