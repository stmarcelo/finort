using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class FluxoPagoTests
{
    private static async Task<(AppDbContext Db, string File, CartaoCredito Cartao, Conta Conta,
        LancamentoService L, FaturaService F)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var cartao = new CartaoCredito
        {
            Banco = "Inter", Ultimos4Digitos = "2222",
            MelhorDiaCompra = 5, DiaVencimento = 20, Limite = 8000m, Ativo = true
        };
        db.CartoesCredito.Add(cartao);
        db.SaveChanges();
        var conta = await new ContaService(db).CriarAsync("Banco", null, null, null);
        return (db, file, cartao, conta, new LancamentoService(db), new FaturaService(db));
    }

    [Fact]
    public async Task Cartao_ComFaturaPaga_VemMarcadoComoPago()
    {
        var (db, file, cartao, conta, l, f) = await SetupAsync();
        try
        {
            await l.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 7, 1), 300m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 7, 10));
            var compra = db.Lancamentos.Single(x => x.CartaoCreditoId == cartao.Id);
            compra.Confirmado = true;
            db.SaveChanges();
            await f.FecharAsync(cartao.Id, 2026, 7);
            await f.PagarAsync(cartao.Id, 2026, 7, conta.Id, new DateOnly(2026, 7, 25), 300m);

            var fluxoJulho = await new FluxoService(db).ObterCardAsync(2026, 7);
            Assert.True(fluxoJulho.TotaisPorCartao.Single(t => t.CartaoId == cartao.Id).Pago);

            var fluxoAgosto = await new FluxoService(db).ObterCardAsync(2026, 8);
            Assert.DoesNotContain(fluxoAgosto.TotaisPorCartao, t => t.CartaoId == cartao.Id && t.Pago);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Cartao_SemFaturaFechada_NaoVemPago()
    {
        var (db, file, cartao, _, l, _) = await SetupAsync();
        try
        {
            await l.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 7, 1), 300m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 7, 10));

            var fluxo = await new FluxoService(db).ObterCardAsync(2026, 7);
            Assert.False(fluxo.TotaisPorCartao.Single(t => t.CartaoId == cartao.Id).Pago);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task TodosConfirmados_MarcaReceitasEDespesasPagas()
    {
        var (db, file, _, conta, l, _) = await SetupAsync();
        try
        {
            var categoriaId = db.Categorias.First().Id;
            await l.CriarReceitaAsync(conta.Id, new DateOnly(2026, 7, 5), 1000m, categoriaId, null, null);
            await l.CriarDespesaAsync(conta.Id, new DateOnly(2026, 7, 6), 200m, categoriaId, null, null);
            foreach (var lancamento in db.Lancamentos.Where(x => x.ContaId == conta.Id))
                lancamento.Confirmado = true;
            db.SaveChanges();

            var fluxo = await new FluxoService(db).ObterCardAsync(2026, 7);

            Assert.True(fluxo.ReceitasPagas);
            Assert.True(fluxo.DespesasPagas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ParcialmenteConfirmados_NaoMarca()
    {
        var (db, file, _, conta, l, _) = await SetupAsync();
        try
        {
            var categoriaId = db.Categorias.First().Id;
            await l.CriarDespesaAsync(conta.Id, new DateOnly(2026, 7, 6), 200m, categoriaId, null, null);
            await l.CriarDespesaAsync(conta.Id, new DateOnly(2026, 7, 7), 300m, categoriaId, null, null);
            db.Lancamentos.OrderBy(x => x.Data).First(x => x.Tipo == LancamentoTipo.Despesa).Confirmado = true;
            db.SaveChanges();

            var fluxo = await new FluxoService(db).ObterCardAsync(2026, 7);

            Assert.False(fluxo.DespesasPagas);
            Assert.False(fluxo.ReceitasPagas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
