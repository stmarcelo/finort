using Finort.Data;
using Microsoft.Data.Sqlite;

namespace Finort.Tests;

public class AuthMigrationTests
{
    [Fact]
    public void Migrate_CreatesConfiguracaoAndPasswordResetTokenTables()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var tables = new List<string>();
            using (var conn = new SqliteConnection($"Data Source={file}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT name FROM sqlite_master WHERE type='table' " +
                    "AND name IN ('Configuracoes','PasswordResetTokens')";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) tables.Add(reader.GetString(0));
            }

            Assert.Contains("Configuracoes", tables);
            Assert.Contains("PasswordResetTokens", tables);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}