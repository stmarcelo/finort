using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class CartaoLimiteDisponivelTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService LancamentoService, CartaoCreditoService CartaoService, CartaoCredito Cartao)> SetupAsync(decimal limite)
    {
        var (db, file) = TestDbContext.Create();
        var cartao = new CartaoCredito
        {
            Banco = "Teste", Ultimos4Digitos = "9999",
            MelhorDiaCompra = 5, DiaVencimento = 10, Limite = limite, Ativo = true
        };
        db.CartoesCredito.Add(cartao);
        await db.SaveChangesAsync();
        return (db, file, new LancamentoService(db), new CartaoCreditoService(db), cartao);
    }

    private static Categoria Renda(AppDbContext db) => db.Categorias.First(c => c.Nome == "Receita");

    [Fact]
    public async Task LimiteDisponivel_ConsideraTodosNaoPagosConfirmadosOuNao()
    {
        var (db, file, lancamentoService, cartaoService, cartao) = await SetupAsync(200m);
        try
        {
            // saída 5,00 (mês 8) — confirmada
            var saida = (await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 5), 5m,
                Renda(db).Id, null, null, parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 8, 5)))[0];
            await lancamentoService.AlternarConfirmadoAsync(saida.Id);

            // entrada 0,10 (mês 8) confirmado
            var entrada = (await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 0.10m,
                Renda(db).Id, null, null, parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 8, 6), ehEntrada: true))[0];
            await lancamentoService.AlternarConfirmadoAsync(entrada.Id);

            // saída parcela 1 e 2 (mês 9)
            await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 9, 1), 150m,
                Renda(db).Id, null, null, parcelas: 2, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 9, 1));

            var resumo = (await cartaoService.ListarComSaldoAsync()).Single();

            // limite 200 - (5 + 0,10 + 75 + 75) = 44,90; entrada 0,10 SOMA ao limite: 200 - 154,90 = 45,10
            Assert.Equal(-154.90m, resumo.TotalNaoPago);
            Assert.Equal(45.10m, resumo.LimiteDisponivel);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task LimiteDisponivel_NaoContaProvisaoNemPagamento()
    {
        var (db, file, lancamentoService, cartaoService, cartao) = await SetupAsync(1000m);
        try
        {
            await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 5), 100m,
                Renda(db).Id, null, null, parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 8, 5));

            // pagamento de fatura (transferência com CartaoCreditoId e Valor > 0) não deve consumir limite
            var conta = new Conta { Nome = "Conta" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var pagamentoSub = db.Subcategorias.First(s => s.IsProtected && s.Nome == "Cartão de crédito");
            var faturaService = new FaturaService(db);
            var despesa = db.Lancamentos.Single(l => l.CartaoCreditoId == cartao.Id);
            await lancamentoService.AlternarConfirmadoAsync(despesa.Id);
            await faturaService.FecharAsync(cartao.Id, 2026, 8);
            await faturaService.PagarAsync(cartao.Id, 2026, 8, conta.Id, new DateOnly(2026, 8, 10), 100m);

            var resumo = (await cartaoService.ListarComSaldoAsync()).Single();
            Assert.Equal(1000m, resumo.LimiteDisponivel);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task TotalNaoPago_ExcluiFaturaPaga_PorMesDoVencimentoNaoDaCompra()
    {
        var (db, file, lancamentoService, cartaoService, cartao) = await SetupAsync(2000m);
        try
        {
            // compra em setembro com fatura (vencimento) em outubro
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 9, 10),
                Tipo = LancamentoTipo.Despesa,
                Valor = -100m,
                CartaoCreditoId = cartao.Id,
                DataVencimentoCartao = new DateOnly(2026, 10, 10),
                CategoriaId = Renda(db).Id,
                Confirmado = true
            });
            var conta = new Conta { Nome = "Conta" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();

            var faturaService = new FaturaService(db);
            await faturaService.FecharAsync(cartao.Id, 2026, 10);
            await faturaService.PagarAsync(cartao.Id, 2026, 10, conta.Id, new DateOnly(2026, 10, 10), 100m);

            var resumo = (await cartaoService.ListarComSaldoAsync()).Single();

            // fatura 10 paga -> compra (Data em mês 9, vencimento em mês 10) sai do TotalNaoPago
            Assert.Equal(0m, resumo.TotalNaoPago);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task TotalNaoPago_MantemLancamentoDeFaturaAberta_NoMesPago()
    {
        var (db, file, lancamentoService, cartaoService, cartao) = await SetupAsync(2000m);
        try
        {
            // compra da fatura 09 (vence 05/09)
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 9, 5),
                Tipo = LancamentoTipo.Despesa,
                Valor = -50m,
                CartaoCreditoId = cartao.Id,
                DataVencimentoCartao = new DateOnly(2026, 9, 5),
                CategoriaId = Renda(db).Id,
                Confirmado = true
            });
            // compra da fatura 10 (vence 15/10), mesma Data month 09 — não pode ser excluída
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 9, 20),
                Tipo = LancamentoTipo.Despesa,
                Valor = -30m,
                CartaoCreditoId = cartao.Id,
                DataVencimentoCartao = new DateOnly(2026, 10, 15),
                CategoriaId = Renda(db).Id,
                Confirmado = true
            });
            var conta = new Conta { Nome = "Conta" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();

            var faturaService = new FaturaService(db);
            await faturaService.FecharAsync(cartao.Id, 2026, 9);
            await faturaService.PagarAsync(cartao.Id, 2026, 9, conta.Id, new DateOnly(2026, 9, 10), 50m);

            var resumo = (await cartaoService.ListarComSaldoAsync()).Single();

            // fatura 09 paga exclui só a compra de 05/09; a de 20/09 (fatura 10 aberta) permanece
            Assert.Equal(-30m, resumo.TotalNaoPago);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
