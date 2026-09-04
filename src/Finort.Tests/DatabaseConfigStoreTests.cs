using Finort.Models.Configuration;
using Finort.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Finort.Tests;

public class DatabaseConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cf-store-{Guid.NewGuid():N}");
    private readonly string _appSettings;

    public DatabaseConfigStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _appSettings = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(_appSettings,
            """{ "Database": { "Provider": "Sqlite", "Sqlite": { "ConnectionString": "Data Source=origem.db" } } }""");
    }

    [Fact]
    public void Get_SemArquivoProprio_UsaAppsettings()
    {
        var store = new DatabaseConfigStore(_dir);
        Assert.Equal("Sqlite", store.Get().Provider);
        Assert.Equal("Data Source=origem.db", store.Get().Sqlite.ConnectionString);
    }

    [Fact]
    public void Set_PersisteERecarregaProvider()
    {
        var store = new DatabaseConfigStore(_dir);
        var config = new DatabaseConfig
        {
            Provider = "MySql",
            MySql = new DatabaseConnectionSettings { ConnectionString = "Server=x" }
        };
        store.Set(config);

        var reload = new DatabaseConfigStore(_dir);
        Assert.Equal("MySql", reload.Get().Provider);
        Assert.Equal("Server=x", reload.Get().MySql.ConnectionString);
    }

    private static SecretProtector CriarProtector(string aplicacao)
    {
        var provider = new ServiceCollection()
            .AddDataProtection()
            .SetApplicationName(aplicacao)
            .Services
            .BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();
        return new SecretProtector(provider);
    }

    [Fact]
    public void Get_BlobIlegivel_KeyringPerdido_MantemValorBrutoSemLancar()
    {
        var protegida = CriarProtector("finort-keyring-perdido").Protect("Server=producao;Password=segredo");
        File.WriteAllText(Path.Combine(_dir, "database.settings.json"),
            "{\"Provider\":\"MySql\",\"MySql\":{\"ConnectionString\":\"" + protegida + "\"}}");

        var reload = new DatabaseConfigStore(_dir, CriarProtector("finort-keyring-novo"));

        var config = reload.Get();
        Assert.Equal(protegida, config.MySql.ConnectionString);
        Assert.StartsWith(SecretProtector.Prefixo, config.MySql.ConnectionString);
    }

    [Fact]
    public void Get_ValorEmTextoPlano_ComProtector_MantemValor()
    {
        File.WriteAllText(Path.Combine(_dir, "database.settings.json"),
            """{"Provider":"MySql","MySql":{"ConnectionString":"Server=x"}}""");

        var reload = new DatabaseConfigStore(_dir, CriarProtector("finort-texto-plano"));

        Assert.Equal("Server=x", reload.Get().MySql.ConnectionString);
    }

    [Fact]
    public void Get_ComFinortDataDir_RedesinaCaminhoSqlite()
    {
        var dataDir = Path.Combine(_dir, "data");
        Directory.CreateDirectory(dataDir);
        var original = Environment.GetEnvironmentVariable("FINORT_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("FINORT_DATA_DIR", dataDir);
            var store = new DatabaseConfigStore(_dir);
            var config = store.Get();
            Assert.Equal($"Data Source={Path.Combine(dataDir, "origem.db")}", config.Sqlite.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FINORT_DATA_DIR", original);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
