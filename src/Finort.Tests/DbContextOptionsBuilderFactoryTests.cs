using Finort.Data;
using Finort.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Finort.Tests;

public class DbContextOptionsBuilderFactoryTests
{
    [Fact]
    public void Configure_Sqlite_AddsSqliteProvider()
    {
        var config = new DatabaseConfig
        {
            Provider = "Sqlite",
            Sqlite = new DatabaseConnectionSettings { ConnectionString = "Data Source=:memory:" }
        };

        var options = DbContextOptionsBuilderFactory.Build(config);

        var ext = options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault();
        Assert.NotNull(ext);
        Assert.Equal("Data Source=:memory:", ext!.ConnectionString);
    }

    [Fact]
    public void Configure_MySql_AddsMySqlProvider()
    {
        var config = new DatabaseConfig
        {
            Provider = "MySql",
            MySql = new DatabaseConnectionSettings { ConnectionString = "Server=localhost;" }
        };
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DbContextOptionsBuilderFactory.Configure(
            builder, config, new MySqlServerVersion(new Version(8, 0, 36)));

        var ext = builder.Options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault();
        Assert.NotNull(ext);
        Assert.Equal("Server=localhost;", ext!.ConnectionString);
    }

    [Fact]
    public void BuildGenerico_MySql_RetornaOptionsTipadas()
    {
        var config = new DatabaseConfig
        {
            Provider = "MySql",
            MySql = new DatabaseConnectionSettings { ConnectionString = "Server=localhost;Database=t;Uid=u;Pwd=p;" }
        };
        var options = DbContextOptionsBuilderFactory.Build<MySqlAppDbContext>(
            config, new MySqlServerVersion(new Version(8, 0, 36)));
        Assert.IsType<DbContextOptions<MySqlAppDbContext>>(options);
    }

    [Fact]
    public void BuildGenerico_Sqlite_RetornaOptionsTipadas()
    {
        var config = new DatabaseConfig
        {
            Provider = "Sqlite",
            Sqlite = new DatabaseConnectionSettings { ConnectionString = "Data Source=:memory:" }
        };
        var options = DbContextOptionsBuilderFactory.Build<AppDbContext>(config);
        Assert.IsType<DbContextOptions<AppDbContext>>(options);
    }
}