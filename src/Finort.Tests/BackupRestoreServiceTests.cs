using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class BackupRestoreServiceTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cf-bkp-{Guid.NewGuid():N}");

    public BackupRestoreServiceTests() => Directory.CreateDirectory(_dir);

    private BackupRestoreService CriarServico(string senhaBackupCadastrada = "bkpsenha1")
    {
        var auth = new AuthService(_ctx.Db);
        var config = auth.CriarConfiguracaoAsync("Eu", "eu@x.com", "login123").GetAwaiter().GetResult();
        auth.DefinirSenhaBackupAsync(config, senhaBackupCadastrada).GetAwaiter().GetResult();

        var store = new DatabaseConfigStore(_dir);
        store.Set(new Models.Configuration.DatabaseConfig
        {
            Provider = "Sqlite",
            Sqlite = new() { ConnectionString = $"Data Source={_ctx.File}" }
        });
        return new BackupRestoreService(store, auth);
    }

    [Fact]
    public async Task Roundtrip_BackupRestauraDados()
    {
        var svc = CriarServico();
        _ctx.Db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Dado Original" });
        _ctx.Db.SaveChanges();

        using var saida = new MemoryStream();
        var gerou = await svc.GerarBackupAsync(saida, "bkpsenha1");
        Assert.True(gerou.Ok, gerou.Erro);

        _ctx.Db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "DADO A DESCARTAR" });
        _ctx.Db.SaveChanges();

        var validacao = await svc.ValidarBackupAsync(saida.ToArray(), "bkpsenha1");
        Assert.True(validacao.Ok, validacao.Erro);

        var restaurou = await svc.RestaurarAsync(saida.ToArray(), "bkpsenha1");
        Assert.True(restaurou.Ok, restaurou.Erro);

        using var recarregado = new AppDbContext(DbContextOptionsBuilderFactory.Build<AppDbContext>(
            new Models.Configuration.DatabaseConfig
            {
                Provider = "Sqlite",
                Sqlite = new() { ConnectionString = $"Data Source={_ctx.File}" }
            }));
        var pessoas = recarregado.Pessoas.Select(p => p.Nome).ToList();
        Assert.Contains("Dado Original", pessoas);
        Assert.DoesNotContain("DADO A DESCARTAR", pessoas);
    }

    [Fact]
    public async Task Validar_SenhaErrada_Falha()
    {
        var svc = CriarServico();
        using var saida = new MemoryStream();
        await svc.GerarBackupAsync(saida, "bkpsenha1");
        var v = await svc.ValidarBackupAsync(saida.ToArray(), "errada");
        Assert.False(v.Ok);
    }

    [Fact]
    public async Task Gerar_SenhaDivergenteDoCadastro_Falha()
    {
        var svc = CriarServico();
        using var saida = new MemoryStream();
        var r = await svc.GerarBackupAsync(saida, "outraSenha");
        Assert.False(r.Ok);
    }

    public void Dispose()
    {
        _ctx.Db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }
}
