using Finort.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Finort.Data;

public class MySqlAppDbContextFactory : IDesignTimeDbContextFactory<MySqlAppDbContext>
{
    public MySqlAppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var database = configuration.GetSection("Database").Get<DatabaseConfig>() ?? new DatabaseConfig();
        var builder = new DbContextOptionsBuilder<MySqlAppDbContext>();
        builder.UseMySql(database.MySql.ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)));

        return new MySqlAppDbContext(builder.Options);
    }
}
