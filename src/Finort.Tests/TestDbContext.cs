using Finort.Data;
using Finort.Models.Configuration;
using Finort.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Finort.Tests;

public static class TestDbContext
{
    public static (AppDbContext Db, string File) Create()
    {
        var file = Path.Combine(Path.GetTempPath(), $"cf-test-{Guid.NewGuid():N}.db");
        var config = new DatabaseConfig
        {
            Provider = "Sqlite",
            Sqlite = new DatabaseConnectionSettings { ConnectionString = $"Data Source={file}" }
        };
        var options = DbContextOptionsBuilderFactory.Build(config);
        var db = new AppDbContext(options);
        new DatabaseMigrator(db, NullLogger<DatabaseMigrator>.Instance).Migrate();
        return (db, file);
    }

    public static void Cleanup(AppDbContext db, string file)
    {
        db.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(file)) File.Delete(file);
    }
}