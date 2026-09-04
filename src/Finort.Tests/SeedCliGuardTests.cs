using Finort.Models.Configuration;
using Finort.Services;

namespace Finort.Tests;

public class SeedCliGuardTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cf-seedguard-{Guid.NewGuid():N}");

    public SeedCliGuardTests() => Directory.CreateDirectory(_dir);

    private static DatabaseConfig ConfigSqlite(string arquivo) => new()
    {
        Provider = "Sqlite",
        Sqlite = new DatabaseConnectionSettings { ConnectionString = $"Data Source={arquivo}" }
    };

    [Fact]
    public void Verificar_Mysql_RetornaAvisoApenasSqlite()
    {
        var config = new DatabaseConfig { Provider = "MySql" };
        Assert.Equal("O seed de teste é suportado apenas para SQLite.",
            SeedCliGuard.Verificar(config, _dir));
    }

    [Fact]
    public void Verificar_ArquivoDbExistente_RetornaAviso()
    {
        var caminho = Path.Combine(_dir, "finort.db");
        File.WriteAllText(caminho, "x");
        var aviso = SeedCliGuard.Verificar(ConfigSqlite(caminho), _dir);
        Assert.NotNull(aviso);
        Assert.Contains("finort.db", aviso);
    }

    [Fact]
    public void Verificar_ArquivoInexistente_RetornaNull()
    {
        Assert.Null(SeedCliGuard.Verificar(
            ConfigSqlite(Path.Combine(_dir, "novo.db")), _dir));
    }

    [Fact]
    public void Verificar_CaminhoRelativo_ResolvidoContraContentRoot()
    {
        File.WriteAllText(Path.Combine(_dir, "finort.db"), "x");
        Assert.NotNull(SeedCliGuard.Verificar(ConfigSqlite("finort.db"), _dir));
    }

    [Fact]
    public void ResolverCaminhoBanco_Relativo_RetornaCaminhoAbsoluto()
    {
        var resultado = SeedCliGuard.ResolverCaminhoBanco("Data Source=finort.db", _dir);
        Assert.Equal(Path.GetFullPath(Path.Combine(_dir, "finort.db")), resultado);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
