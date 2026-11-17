using Microsoft.Data.Sqlite;

namespace Finort.Tests;

public class ProjetoPrazoMigrationTests
{
    [Fact]
    public void Migrate_CriaColunaPrazoEmProjetos()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            using var conn = new SqliteConnection($"Data Source={file}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Projetos') WHERE name='Prazo'";
            Assert.Equal(1L, (long)(cmd.ExecuteScalar() ?? 0L));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
