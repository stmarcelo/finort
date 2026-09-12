using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ReembolsoLeiturasTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Lancamentos, FaturaService Faturas, FluxoService Fluxo, CalendarioService Calendario, ReceitaRelatorioService Receitas, CartaoCredito Cartao, Conta Conta, Categoria CatReceita)> SetupAsync()
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
        var lanc = new LancamentoService(db);
        var fat = new FaturaService(db);
        return (db, file, lanc, fat,
            new FluxoService(db),
            new CalendarioService(db, fat, new LembreteService(db)),
            new ReceitaRelatorioService(db, new TestWebHostEnvironment()),
            cartao, conta, catReceita);
    }

    [Fact]
    public async Task Fluxo_Inclui_ReembolsoPendente_PorVencimento()
    {
        var (db, file, lancamentos, _, fluxo, _, _, cartao, _, catReceita) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            // compra 06/08 -> vencimento cartao 10/09, reembolso vence 09/09
            await lancamentos.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 100m, catReceita.Id, null, null, null, pessoa.Id, null);

            var reembolso = Assert.Single(db.Reembolsos.ToList());
            Assert.Equal(pessoa.Id, reembolso.PessoaId);
            Assert.Equal(new DateOnly(2026, 9, 9), reembolso.Vencimento);
            Assert.False(reembolso.Fechado);

            var fluxoSet = await fluxo.ObterCardAsync(2026, 9);
            Assert.Equal(100m, fluxoSet.TotalReembolsos);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Calendario_Inclui_ReembolsoPendente()
    {
        var (db, file, lancamentos, faturas, _, calendario, _, cartao, _, catReceita) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            await lancamentos.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 100m, catReceita.Id, null, null, null, pessoa.Id, null);

            var cal = await calendario.ObterMesAsync(2026, 9);
            var item = Assert.Single(cal.Dias.SelectMany(d => d.Itens), i => i.Valor == 100m && i.Tipo == LancamentoTipo.Receita);
            Assert.Equal(new DateOnly(2026, 9, 9), cal.Dias.First(d => d.Itens.Contains(item)).Data);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ReceitaRelatorio_NaoInclui_Pendente_SoAposFechar()
    {
        var (db, file, lancamentos, faturas, _, _, receitaService, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var criados = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 100m, catReceita.Id, null, null, null, pessoa.Id, null);
            foreach (var d in criados) await lancamentos.AlternarConfirmadoAsync(d.Id);

            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);
            var antes = await receitaService.GerarAsync(inicio, fim, null);
            Assert.Equal(0m, antes.TotalConfirmado + antes.TotalNaoConfirmado);

            await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);

            var depois = await receitaService.GerarAsync(inicio, fim, null);
            Assert.Equal(100m, depois.TotalConfirmado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
