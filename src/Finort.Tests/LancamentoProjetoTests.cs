using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class LancamentoProjetoTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly LancamentoService _svc;

    public LancamentoProjetoTests()
    {
        _svc = new LancamentoService(_ctx.Db);
        _ctx.Db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Cliente" });
        _ctx.Db.Contas.Add(new Models.Financeiro.Conta { Nome = "Banco" });
        _ctx.Db.SaveChanges();
    }

    private Guid PessoaId() => _ctx.Db.Pessoas.Single().Id;
    private Guid ContaId() => _ctx.Db.Contas.Single().Id;
    private Guid CatRendaId() => _ctx.Db.Categorias.First(c => c.Nome == "Receita").Id;

    [Fact]
    public async Task CriarReceita_GravaProjeto()
    {
        var projeto = await new ProjetoService(_ctx.Db)
            .CriarAsync("P1", new DateOnly(2026, 1, 1), 100m, PessoaId());

        var lancamento = await _svc.CriarReceitaAsync(ContaId(), new DateOnly(2026, 2, 1), 50m,
            CatRendaId(), null, PessoaId(), projeto.Id);

        Assert.Equal(projeto.Id, lancamento.ProjetoId);
    }

    [Fact]
    public async Task AtualizarReceitaDespesa_ReembolsoHerdaProjeto()
    {
        var svcProjeto = new ProjetoService(_ctx.Db);
        var projeto = await svcProjeto.CriarAsync("P2", new DateOnly(2026, 1, 1), 100m, PessoaId());

        var despesa = await _svc.CriarDespesaCartaoAsync(
            CartaoId(), new DateOnly(2026, 2, 10), 90m, CatAlimentacaoId(), null, PessoaId(),
            parcelas: null, reembolsoPessoaId: PessoaId(), reembolsoVencimento: new DateOnly(2026, 2, 15),
            projetoId: projeto.Id);

        Assert.All(despesa, d => Assert.Equal(projeto.Id, d.ProjetoId));

        var reembolsoIds = despesa.Select(d => d.ReembolsoId!.Value).Distinct().ToList();
        Assert.All(reembolsoIds, id =>
        {
            var reembolso = _ctx.Db.Lancamentos.Find(id)!;
            Assert.Equal(projeto.Id, reembolso.ProjetoId);
        });
    }

    private Guid CartaoId()
    {
        var cartao = new Models.Financeiro.CartaoCredito
        {
            Banco = "Nu", Ultimos4Digitos = "1234", MelhorDiaCompra = 5, DiaVencimento = 15,
            Limite = 1000m, Ativo = true, ContaId = ContaId()
        };
        _ctx.Db.CartoesCredito.Add(cartao);
        _ctx.Db.SaveChanges();
        return cartao.Id;
    }

    private Guid CatAlimentacaoId() => _ctx.Db.Categorias.First(c => c.Nome == "Alimentação").Id;

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
