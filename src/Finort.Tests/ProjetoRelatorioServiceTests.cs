using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class ProjetoRelatorioServiceTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly ProjetoRelatorioService _svc;

    public ProjetoRelatorioServiceTests()
    {
        _svc = new ProjetoRelatorioService(_ctx.Db);
    }

    [Fact]
    public async Task Gerar_TotaisLinhasEFatias_IgnoraLancamentosSemProjeto()
    {
        var db = _ctx.Db;
        var pessoa = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Acme" }).Entity;
        var conta = db.Contas.Add(new Models.Financeiro.Conta { Nome = "Banco" }).Entity;
        db.SaveChanges();
        var projeto = await new ProjetoService(db).CriarAsync("Site", new DateOnly(2026, 1, 5), 10000m, pessoa.Id);
        var renda = db.Categorias.First(c => c.Nome == "Receita");
        var mercadoSub = db.Subcategorias.First(s => s.Nome == "Mercado");
        var alimentacao = db.Categorias.First(c => c.Nome == "Alimentação");

        db.Lancamentos.AddRange(
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 2, 1), Tipo = Models.Financeiro.LancamentoTipo.Receita, Valor = 3000m,
                ContaId = conta.Id, CategoriaId = renda.Id, PessoaId = pessoa.Id, ProjetoId = projeto.Id
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 2, 3), Tipo = Models.Financeiro.LancamentoTipo.Despesa, Valor = -100m,
                ContaId = conta.Id, CategoriaId = alimentacao.Id, SubcategoriaId = mercadoSub.Id,
                PessoaId = pessoa.Id, ProjetoId = projeto.Id
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 2, 4), Tipo = Models.Financeiro.LancamentoTipo.Despesa, Valor = -100m,
                ContaId = conta.Id, CategoriaId = alimentacao.Id, SubcategoriaId = mercadoSub.Id,
                PessoaId = pessoa.Id, ProjetoId = projeto.Id
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 2, 2), Tipo = Models.Financeiro.LancamentoTipo.Despesa, Valor = -50m,
                ContaId = conta.Id, CategoriaId = renda.Id, PessoaId = pessoa.Id
            });

        db.SaveChanges();

        var r = await _svc.GerarAsync(projeto.Id);

        Assert.NotNull(r);
        Assert.Equal("Acme", r!.PessoaNome);
        Assert.Equal(3000m, r.TotalReceitas);
        Assert.Equal(200m, r.TotalDespesas);
        Assert.Equal(2800m, r.Resultado);
        Assert.Equal(3, r.Linhas.Count);
        Assert.Equal([new(2026,2,1), new(2026,2,3), new(2026,2,4)], r.Linhas.Select(l => l.Data));

        var fatia = Assert.Single(r.DespesasPorCategoria);
        Assert.Equal("Alimentação > Mercado", fatia.Rotulo);
        Assert.Equal(200m, fatia.Valor);
        Assert.Equal(100.0, fatia.Percentual);
    }

    [Fact]
    public async Task Gerar_IdInexistente_ReturnNull()
    {
        Assert.Null(await _svc.GerarAsync(Guid.NewGuid()));
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
