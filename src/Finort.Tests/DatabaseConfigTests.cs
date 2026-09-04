using Finort.Models.Configuration;
using Microsoft.Extensions.Configuration;

namespace Finort.Tests;

public class DatabaseConfigTests
{
    [Fact]
    public void Bind_WithValidSection_BindsProviderAndMySql()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "MySql",
                ["Database:MySql:ConnectionString"] = "Server=localhost;Port=3306;Database=cf;User=root;Password=123;"
            })
            .Build();

        var database = configuration.GetSection("Database").Get<DatabaseConfig>();

        Assert.NotNull(database);
        Assert.Equal("MySql", database!.Provider);
        Assert.Equal("Server=localhost;Port=3306;Database=cf;User=root;Password=123;", database.MySql.ConnectionString);
        Assert.Equal("", database.Sqlite.ConnectionString);
    }

    [Fact]
    public void Bind_WithoutSection_DefaultsToSqlite()
    {
        var configuration = new ConfigurationBuilder().Build();

        var database = configuration.GetSection("Database").Get<DatabaseConfig>()
                       ?? new DatabaseConfig();

        Assert.Equal("Sqlite", database.Provider);
    }
}