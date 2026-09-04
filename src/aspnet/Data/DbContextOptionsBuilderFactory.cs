using Finort.Models.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Finort.Data;

public static class DbContextOptionsBuilderFactory
{
    public static DbContextOptions<AppDbContext> Build(DatabaseConfig config)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        Configure(builder, config);
        return builder.Options;
    }

    public static DbContextOptions<TContext> Build<TContext>(
        DatabaseConfig config,
        ServerVersion? mysqlServerVersion = null) where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        Configure(builder, config, mysqlServerVersion);
        return builder.Options;
    }

    public static void Configure(
        DbContextOptionsBuilder builder,
        DatabaseConfig config,
        ServerVersion? mysqlServerVersion = null)
    {
        if (string.Equals(config.Provider, "MySql", StringComparison.OrdinalIgnoreCase))
        {
            mysqlServerVersion ??= ServerVersion.AutoDetect(config.MySql.ConnectionString);
            builder.UseMySql(config.MySql.ConnectionString, mysqlServerVersion);
        }
        else
        {
            builder.UseSqlite(config.Sqlite.ConnectionString);
        }
    }
}