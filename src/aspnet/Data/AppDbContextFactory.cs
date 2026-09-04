using Finort.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Finort.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var database = configuration.GetSection("Database").Get<DatabaseConfig>() ?? new DatabaseConfig();
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DbContextOptionsBuilderFactory.Configure(
            builder,
            database,
            new MySqlServerVersion(new Version(8, 0, 36)));

        return new AppDbContext(builder.Options);
    }
}