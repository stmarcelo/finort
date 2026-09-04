using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class ProjetoServiceTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly ProjetoService _svc;

    public ProjetoServiceTests()
    {
        _svc = new ProjetoService(_ctx.Db);
        _ctx.Db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "João" });
        _ctx.Db.SaveChanges();
    }

    private Guid PessoaId() => _ctx.Db.Pessoas.First(p => p.Nome == "João").Id;

    [Fact]
    public async Task Listar_OrdenaDataDesc()
    {
        await _svc.CriarAsync("Antigo", new DateOnly(2025, 1, 10), 100m, PessoaId());
        await _svc.CriarAsync("Recente", new DateOnly(2026, 5, 2), 200m, PessoaId());

        var lista = await _svc.ListarAsync();
        Assert.Equal(["Recente", "Antigo"], lista.Select(p => p.Descricao));
    }

    [Fact]
    public async Task ListarPorPessoa_FiltraEOrdena()
    {
        var outro = _ctx.Db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Maria" });
        await _ctx.Db.SaveChangesAsync();
        await _svc.CriarAsync("A", new DateOnly(2026, 1, 1), 10m, PessoaId());
        await _svc.CriarAsync("B", new DateOnly(2026, 2, 1), 20m, outro.Entity.Id);

        var joao = await _svc.ListarPorPessoaAsync(PessoaId());
        Assert.Single(joao);
        Assert.Equal("A", joao[0].Descricao);
        Assert.Equal(1, await _svc.ContarPorPessoaAsync(PessoaId()));
    }

    [Fact]
    public async Task Excluir_ComLancamentos_Lanca()
    {
        var projeto = await _svc.CriarAsync("X", new DateOnly(2026, 1, 1), 10m, PessoaId());
        var conta = _ctx.Db.Contas.Add(new Models.Financeiro.Conta { Nome = "Conta" });
        await _ctx.Db.SaveChangesAsync();
        _ctx.Db.Lancamentos.Add(new Models.Financeiro.Lancamento
        {
            Data = new DateOnly(2026, 1, 2),
            Tipo = Models.Financeiro.LancamentoTipo.Despesa,
            Valor = -5m,
            ContaId = conta.Entity.Id,
            CategoriaId = _ctx.Db.Categorias.First().Id,
            ProjetoId = projeto.Id
        });
        await _ctx.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.ExcluirAsync(projeto.Id));
        Assert.Contains("lançamentos vinculados", ex.Message);
    }

    [Fact]
    public async Task Excluir_SemLancamentos_Remove()
    {
        var projeto = await _svc.CriarAsync("Y", new DateOnly(2026, 1, 1), 10m, PessoaId());
        await _svc.ExcluirAsync(projeto.Id);
        Assert.False(_ctx.Db.Projetos.Any(p => p.Id == projeto.Id));
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
