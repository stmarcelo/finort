using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class DashboardReembolsoTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Lancamentos, FaturaService Faturas, DashboardService Dashboard, CartaoCredito Cartao, Conta Conta, Categoria CatReceita)> SetupAsync()
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
        return (db, file, new LancamentoService(db), new FaturaService(db),
            new DashboardService(db, new CartaoCreditoService(db)), cartao, conta, catReceita);
    }

    [Fact]
    public async Task Obter_IgnoraDespesaCartaoComReembolso_MasMantemSemReembolso()
    {
        var (db, file, lancamentos, _, dashboard, cartao, _, catReceita) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            // com reembolso -> ignorada; sem reembolso -> conta
            var comReembolso = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoa.Id, null);
            var semReembolso = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 40m, catReceita.Id, null, null, null, null, null);
            foreach (var d in comReembolso.Concat(semReembolso))
                await lancamentos.AlternarConfirmadoAsync(d.Id);

            // dashboard agrega pela Data da compra (agosto), não pelo vencimento da fatura
            var dash = await dashboard.ObterAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

            Assert.Equal(40m, dash.TotalDespesas);
            Assert.DoesNotContain(dash.TopDespesas, t => t.Valor == 100m);
            Assert.Contains(dash.TopDespesas, t => t.Valor == 40m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_IgnoraReceitaDeReembolso()
    {
        var (db, file, lancamentos, faturas, dashboard, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoa.Id, null);
            foreach (var d in d1) await lancamentos.AlternarConfirmadoAsync(d.Id);
            await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);

            var dash = await dashboard.ObterAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

            Assert.Equal(0m, dash.TotalReceitas);
            Assert.Empty(dash.ReceitasPorCategoria);
            Assert.Empty(dash.TopReceitas);
            Assert.Equal(0m, dash.TotalDespesas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Tendencia_IgnoraReembolsos()
    {
        var (db, file, lancamentos, faturas, dashboard, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoa.Id, null);
            foreach (var d in d1) await lancamentos.AlternarConfirmadoAsync(d.Id);
            await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);

            var dash = await dashboard.ObterAsync(inicio, fim);
            var setembro = dash.TendenciaMensal.Single(t => t.Ano == 2026 && t.Mes == 9);

            Assert.Equal(0m, setembro.Receitas);
            Assert.Equal(0m, setembro.Despesas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
