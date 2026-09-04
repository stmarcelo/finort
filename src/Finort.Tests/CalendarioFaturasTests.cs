using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class CalendarioFaturasTests
{
    private static async Task<(AppDbContext Db, string File, CartaoCredito Cartao)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var cartao = new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1111",
            MelhorDiaCompra = 5, DiaVencimento = 15, Limite = 5000m, Ativo = true
        };
        db.CartoesCredito.Add(cartao);
        db.SaveChanges();
        return (db, file, cartao);
    }

    private static async Task<(LancamentoService L, FaturaService F, Conta Conta)> ServicosAsync(AppDbContext db)
        => (new LancamentoService(db), new FaturaService(db),
            await new ContaService(db).CriarAsync("Banco", null, null, null));

    [Fact]
    public async Task FaturaAberta_ApareceNoVencimentoDoMesSeguinte()
    {
        var (db, file, cartao) = await SetupAsync();
        try
        {
            var (l, _, _) = await ServicosAsync(db);
            await l.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 7, 1), 120m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 7, 10));

            var mes = await new CalendarioService(db, new FaturaService(db), new LembreteService(db)).ObterMesAsync(2026, 8);

            var item = mes.Dias.SelectMany(d => d.Itens)
                .Single(i => i.Descricao.StartsWith("Fatura Nubank"));
            Assert.Equal(-120m, item.Valor);
            Assert.False(item.Riscada);
            Assert.Equal(new DateOnly(2026, 8, 15), mes.Dias.First(d => d.Itens.Any(x => x == item)).Data);
            // Informativo: não entra nos totais
            Assert.Equal(0m, mes.TotalApagar);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task FaturaPaga_FicaRiscada()
    {
        var (db, file, cartao) = await SetupAsync();
        try
        {
            var (l, f, conta) = await ServicosAsync(db);
            await l.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 7, 1), 120m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 7, 10));
            var compra = db.Lancamentos.Single(x => x.CartaoCreditoId == cartao.Id);
            compra.Confirmado = true;
            db.SaveChanges();
            await f.FecharAsync(cartao.Id, 2026, 7);
            await f.PagarAsync(cartao.Id, 2026, 7, conta.Id, new DateOnly(2026, 7, 20), 120m);

            var mes = await new CalendarioService(db, new FaturaService(db), new LembreteService(db)).ObterMesAsync(2026, 8);

            var item = mes.Dias.SelectMany(d => d.Itens)
                .Single(i => i.Descricao.StartsWith("Fatura Nubank"));
            Assert.Equal(-120m, item.Valor);
            Assert.True(item.Riscada);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task PagamentoDeFatura_NaoApareceNoCalendario()
    {
        var (db, file, cartao) = await SetupAsync();
        try
        {
            var (l, f, conta) = await ServicosAsync(db);
            await l.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 7, 1), 120m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 7, 10));
            var compra = db.Lancamentos.Single(x => x.CartaoCreditoId == cartao.Id);
            compra.Confirmado = true;
            db.SaveChanges();
            await f.FecharAsync(cartao.Id, 2026, 7);
            await f.PagarAsync(cartao.Id, 2026, 7, conta.Id, new DateOnly(2026, 7, 20), 120m);

            var mes = await new CalendarioService(db, new FaturaService(db), new LembreteService(db)).ObterMesAsync(2026, 7);

            var itens = mes.Dias.SelectMany(d => d.Itens).ToList();
            // única saída do dia é o informativo da fatura; nenhuma perna de transferência aparece
            Assert.DoesNotContain(itens, i => i.LancamentoId is not null && i.Valor <= -100m);
            Assert.Equal(0m, mes.TotalApagar);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task LancamentoDeInvestimento_NaoApareceNoCalendario()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = await new ContaService(db).CriarAsync("Banco", null, null, null);
            var investimentos = new InvestimentoService(db, new LancamentoService(db));
            var investimento = await investimentos.CriarAsync("Reserva", TipoInvestimento.Reserva,
                conta.Id, null, null, 10m, DateTime.Today);
            var movimento = await investimentos.RegistrarMovimentoAsync(investimento.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 300m);
            var lancamentoDoAporte = movimento.LancamentoId!.Value;

            var mes = await new CalendarioService(db, new FaturaService(db), new LembreteService(db)).ObterMesAsync(2026, 8);

            // calendário mostra só o que entra/sai da conta; aportes não são compromissos bancários
            Assert.DoesNotContain(mes.Dias.SelectMany(d => d.Itens),
                i => i.LancamentoId == lancamentoDoAporte);
            Assert.Equal(0m, mes.TotalApagar); // nada mais no mês
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task MesSemCompras_NaoExibeFatura()
    {
        var (db, file, _) = await SetupAsync();
        try
        {
            var mes = await new CalendarioService(db, new FaturaService(db), new LembreteService(db)).ObterMesAsync(2026, 9);
            Assert.DoesNotContain(mes.Dias.SelectMany(d => d.Itens), i => i.Descricao.StartsWith("Fatura "));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
