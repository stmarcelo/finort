using Finort.Data;
using Finort.Models.Configuration;
using Finort.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Finort.Tests;

public class DatabaseSwitchServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cf-switch-{Guid.NewGuid():N}");
    private readonly AppDbContext _origem;
    private readonly string _destinoPath;

    public DatabaseSwitchServiceTests()
    {
        Directory.CreateDirectory(_dir);
        (_origem, _) = TestDbContext.Create();
        _destinoPath = Path.Combine(_dir, "destino.db");
    }

    private DatabaseConfig ConfigDestino() => new()
    {
        Provider = "Sqlite",
        Sqlite = new DatabaseConnectionSettings { ConnectionString = $"Data Source={_destinoPath}" }
    };

    private DatabaseSwitchService CriarServico(DatabaseConfigStore store)
        => new(_origem, store, NullLogger<DatabaseSwitchService>.Instance);

    [Fact]
    public async Task Executar_CopiaTodosDadosEDestinoZeradoAntes()
    {
        var pessoa = new Models.Financeiro.Pessoa { Nome = "Ana" };
        var conta = new Models.Financeiro.Conta { Nome = "Banco" };
        _origem.Pessoas.Add(pessoa);
        _origem.Contas.Add(conta);
        var projeto = new Models.Financeiro.Projeto
        {
            Descricao = "Reforma",
            Pessoa = pessoa,
            DataContratacao = new DateOnly(2026, 8, 25),
            ValorContratado = 1000m
        };
        _origem.Projetos.Add(projeto);
        _origem.Lancamentos.Add(new Models.Financeiro.Lancamento
        {
            Data = new DateOnly(2026, 8, 25),
            Tipo = Models.Financeiro.LancamentoTipo.Receita,
            Valor = 100m,
            Conta = conta,
            CategoriaId = _origem.Categorias.First(c => c.Nome == "Receita").Id,
            Projeto = projeto
        });
        _origem.SaveChanges();

        var destinoCfg = ConfigDestino();
        var destinoPre = new AppDbContext(DbContextOptionsBuilderFactory.Build<AppDbContext>(destinoCfg));
        new DatabaseMigrator(destinoPre, NullLogger<DatabaseMigrator>.Instance).Migrate();
        destinoPre.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "LIXO-PREEXISTENTE" });
        destinoPre.SaveChanges();
        destinoPre.Dispose();
        SqliteConnection.ClearAllPools();

        var dirStore = new DatabaseConfigStore(_dir);
        var svc = CriarServico(dirStore);
        await svc.ExecutarAsync(destinoCfg);

        var destino = new AppDbContext(DbContextOptionsBuilderFactory.Build<AppDbContext>(destinoCfg));
        Assert.Equal(1, await destino.Pessoas.CountAsync());
        Assert.Equal("Ana", (await destino.Pessoas.SingleAsync()).Nome);
        Assert.Equal(await _origem.Categorias.CountAsync(), await destino.Categorias.CountAsync());
        var projetoDestino = await destino.Projetos.SingleAsync();
        Assert.Equal("Reforma", projetoDestino.Descricao);
        Assert.Equal(pessoa.Id, projetoDestino.PessoaId);
        var lancamentoDestino = await destino.Lancamentos.SingleAsync();
        Assert.Equal(projetoDestino.Id, lancamentoDestino.ProjetoId);
        Assert.Equal("Sqlite", new DatabaseConfigStore(_dir).Get().Provider);
        destino.Dispose();

        Assert.Equal(1, _origem.Pessoas.Count());
    }

    [Fact]
    public async Task Executar_DestinoNuncaMigrado_CriaSchemaECopiaDados()
    {
        _origem.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Bruno" });
        _origem.SaveChanges();

        var destinoCfg = ConfigDestino();
        var svc = CriarServico(new DatabaseConfigStore(_dir));
        await svc.ExecutarAsync(destinoCfg);

        var destino = new AppDbContext(DbContextOptionsBuilderFactory.Build<AppDbContext>(destinoCfg));
        Assert.Equal(1, await destino.Pessoas.CountAsync());
        Assert.Equal("Bruno", (await destino.Pessoas.SingleAsync()).Nome);
        Assert.Equal(await _origem.Categorias.CountAsync(), await destino.Categorias.CountAsync());
        destino.Dispose();

        Assert.Equal("Sqlite", new DatabaseConfigStore(_dir).Get().Provider);
    }

    private sealed class ServicoComFalhaNaCopia : DatabaseSwitchService
    {
        public ServicoComFalhaNaCopia(AppDbContext origem, DatabaseConfigStore store)
            : base(origem, store, NullLogger<DatabaseSwitchService>.Instance) { }

        protected override async Task CopiarDadosAsync(AppDbContext destino, bool ehMySql)
        {
            destino.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "PARCIAL" });
            await destino.SaveChangesAsync();
            throw new IOException("falha simulada durante a cópia");
        }
    }

    [Fact]
    public async Task Executar_FalhaNaCopia_FazRollbackEProviderInalterado()
    {
        var destinoCfg = ConfigDestino();
        var dirStore = new DatabaseConfigStore(_dir);
        var providerOriginal = dirStore.Get().Provider;
        var svc = new ServicoComFalhaNaCopia(_origem, dirStore);

        await Assert.ThrowsAnyAsync<IOException>(() => svc.ExecutarAsync(destinoCfg));

        var destino = new AppDbContext(DbContextOptionsBuilderFactory.Build<AppDbContext>(destinoCfg));
        Assert.Equal(0, await destino.Pessoas.CountAsync());
        Assert.Equal(0, await destino.Configuracoes.CountAsync());
        destino.Dispose();

        Assert.Equal(providerOriginal, dirStore.Get().Provider);
    }

    [Fact]
    public async Task TestarConexao_SqliteValida_NaoLanca()
    {
        var svc = CriarServico(new DatabaseConfigStore(_dir));
        await svc.TestarConexaoAsync(ConfigDestino());
    }

    public void Dispose()
    {
        _origem.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }
}
