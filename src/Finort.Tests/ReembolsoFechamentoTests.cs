using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ReembolsoFechamentoTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Lancamentos, FaturaService Faturas, CartaoCredito Cartao, Conta Conta, Categoria CatReceita)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234", MelhorDiaCompra = 5,
            DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        await db.SaveChangesAsync();
        var catReceita = db.Categorias.First(c => c.Nome == "Receita");
        return (db, file, new LancamentoService(db), new FaturaService(db), cartao, conta, catReceita);
    }

    private static async Task ConfirmarTodosAsync(LancamentoService svc, List<Lancamento> lista)
    {
        foreach (var d in lista) await svc.AlternarConfirmadoAsync(d.Id);
    }

    [Fact]
    public async Task Fechar_ComReembolsos_GeraUmaReceitaPorPessoa()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            var pessoaB = new Pessoa { Nome = "B" };
            db.Pessoas.AddRange(pessoaA, pessoaB);
            await db.SaveChangesAsync();

            // compra 06/08 → vencimento 10/09/2026 (MelhorDia 5, DiaVenc 10)
            var compra = new DateOnly(2026, 8, 6);
            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);

            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            var d2 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 50m, catReceita.Id, null, null, null, pessoaA.Id, null);
            var d3 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 30m, catReceita.Id, null, null, null, pessoaB.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1.Concat(d2).Concat(d3).ToList());

            var fatura = await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);

            Assert.True(fatura.Fechada);
            var receitas = db.Lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita && l.ContaId == conta.Id).ToList();
            Assert.Equal(2, receitas.Count);
            Assert.Equal(150m, receitas.Single(r => r.PessoaId == pessoaA.Id).Valor);
            Assert.Equal(30m, receitas.Single(r => r.PessoaId == pessoaB.Id).Valor);
            Assert.True(db.Reembolsos.All(r => r.Fechado));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Reabrir_ReverteReceitasEReembolsos()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoaA);
            await db.SaveChangesAsync();

            var compra = new DateOnly(2026, 8, 6);
            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);

            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1);

            await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);
            Assert.Equal(1, db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita));

            await faturas.ReabrirAsync(cartao.Id, 2026, 9);

            Assert.Empty(db.Lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita && l.ContaId == conta.Id));
            Assert.True(db.Reembolsos.All(r => !r.Fechado && r.ReceitaId == null));
            Assert.False(await faturas.EhFechadaAsync(cartao.Id, 2026, 9));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
