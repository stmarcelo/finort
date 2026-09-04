using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class FluxoServiceTests
{
    private static Guid CategoriaId(AppDbContext db)
        => db.Categorias.First(c => c.Nome == "Contas de casa").Id;

    private static async Task<(AppDbContext Db, string File, Conta Conta)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Banco" };
        db.Contas.Add(conta);
        await db.SaveChangesAsync();
        return (db, file, conta);
    }

    private static Lancamento Novo(DateOnly data, decimal valor, LancamentoTipo tipo, Guid cat, Guid? conta = null, Guid? cartao = null)
        => new() { Data = data, Valor = valor, Tipo = tipo, CategoriaId = cat, ContaId = conta, CartaoCreditoId = cartao };

    private static CartaoCredito NovoCartao(string banco, string digitos) => new()
    {
        Banco = banco,
        Ultimos4Digitos = digitos,
        MelhorDiaCompra = 1,
        DiaVencimento = 10,
        Limite = 5000m,
        Ativo = true
    };

    [Fact]
    public async Task ObterCardAsync_ExcluiTransferenciaDosTotaisMasContaNoSaldo()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var cat = CategoriaId(db);
            db.Lancamentos.AddRange(
                Novo(inicioMes.AddDays(1), 500m, LancamentoTipo.Receita, cat, conta.Id),
                Novo(inicioMes.AddDays(2), -200m, LancamentoTipo.Despesa, cat, conta.Id),
                Novo(inicioMes.AddDays(3), -100m, LancamentoTipo.Transferencia, cat, conta.Id),
                Novo(inicioMes.AddDays(3), 100m, LancamentoTipo.Transferencia, cat, conta.Id));
            await db.SaveChangesAsync();
            var service = new FluxoService(db);

            var card = await service.ObterCardAsync(hoje.Year, hoje.Month);

            Assert.Equal(500m, card.TotalReceitas);
            Assert.Equal(200m, card.TotalDespesas);
            Assert.Equal(300m, card.SaldoMes);
            Assert.Equal(300m, card.SaldoAcumulado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterCardAsync_ComDoisCartoes_AgrupaPorBancoEUltimosDigitos()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var cat = CategoriaId(db);
            var nubank = NovoCartao("Nubank", "4321");
            var inter = NovoCartao("Inter", "9999");
            db.CartoesCredito.AddRange(nubank, inter);
            await db.SaveChangesAsync();

            // Vencimentos derivados da regra do ciclo: compra dia >= melhorDia 1 → fatura vence mês+2
            var despesaNubank = Novo(inicioMes.AddDays(1), -300m, LancamentoTipo.Despesa, cat, cartao: nubank.Id);
            despesaNubank.DataVencimentoCartao = CartaoCreditoService.CalcularVencimento(nubank, despesaNubank.Data);
            var despesaInter = Novo(inicioMes.AddDays(2), -150m, LancamentoTipo.Despesa, cat, cartao: inter.Id);
            despesaInter.DataVencimentoCartao = CartaoCreditoService.CalcularVencimento(inter, despesaInter.Data);
            db.Lancamentos.AddRange(despesaNubank, despesaInter,
                Novo(inicioMes.AddDays(3), -50m, LancamentoTipo.Despesa, cat, conta.Id));
            await db.SaveChangesAsync();
            var service = new FluxoService(db);

            // TotalDespesas atribui pela data de compra (mês corrente)
            var cardMes = await service.ObterCardAsync(hoje.Year, hoje.Month);
            Assert.Equal(500m, cardMes.TotalDespesas);

            // TotaisPorCartao atribui pela DataVencimentoCartao: faturas vencem 2 meses depois
            var vencimento = despesaNubank.DataVencimentoCartao!.Value;
            var cardFaturas = await service.ObterCardAsync(vencimento.Year, vencimento.Month);
            Assert.Equal(2, cardFaturas.TotaisPorCartao.Count);
            Assert.Contains(cardFaturas.TotaisPorCartao, t => t.Nome == "Nubank ••4321" && t.Total == 300m);
            Assert.Contains(cardFaturas.TotaisPorCartao, t => t.Nome == "Inter ••9999" && t.Total == 150m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterCardAsync_MesFuturo_IncluiApenasProvisoesNaoLancadas()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var alvo = hoje.AddMonths(1);
            var naoLancada = new Provisao
            {
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 15,
                Valor = 400m,
                ValorVariante = false,
                CategoriaId = CategoriaId(db),
                ContaId = conta.Id,
                UltimoMesLancado = hoje.Month,
                UltimoAnoLancado = hoje.Year
            };
            var jaLancada = new Provisao
            {
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 20,
                Valor = 700m,
                ValorVariante = false,
                CategoriaId = CategoriaId(db),
                ContaId = conta.Id,
                UltimoMesLancado = alvo.Month,
                UltimoAnoLancado = alvo.Year
            };
            db.Provisoes.AddRange(naoLancada, jaLancada);
            await db.SaveChangesAsync();
            var service = new FluxoService(db);

            var card = await service.ObterCardAsync(alvo.Year, alvo.Month);

            Assert.Equal(400m, card.TotalDespesas);
            Assert.Equal(-400m, card.SaldoMes);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterCardAsync_SaldoAnterior_IncluiRealEProjecoesAnteriores()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var alvo = hoje.AddMonths(1);
            var cat = CategoriaId(db);
            var mesPassado = hoje.AddMonths(-1);
            db.MesesFechados.Add(new MesFechado { Ano = alvo.Year, Mes = alvo.Month, DataFechamento = DateTime.Now });
            db.Lancamentos.Add(Novo(hoje, 1000m, LancamentoTipo.Receita, cat, conta.Id));
            var provisao = new Provisao
            {
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 5,
                Valor = 100m,
                ValorVariante = false,
                CategoriaId = cat,
                ContaId = conta.Id,
                UltimoMesLancado = mesPassado.Month,
                UltimoAnoLancado = mesPassado.Year
            };
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();
            var service = new FluxoService(db);

            var card = await service.ObterCardAsync(alvo.Year, alvo.Month);

            Assert.Equal(900m, card.SaldoAnterior);
            Assert.Equal(0m, card.TotalReceitas);
            Assert.Equal(0m, card.TotalDespesas);
            Assert.Equal(900m, card.SaldoAcumulado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterCardAsync_SaldoAcumulado_EhAnteriorMaisSaldoMes()
    {
        var (db, file, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var inicioMesPassado = inicioMes.AddMonths(-1);
            var cat = CategoriaId(db);
            db.Lancamentos.AddRange(
                Novo(inicioMesPassado.AddDays(1), 500m, LancamentoTipo.Receita, cat, conta.Id),
                Novo(inicioMes.AddDays(1), 200m, LancamentoTipo.Receita, cat, conta.Id),
                Novo(inicioMes.AddDays(2), -80m, LancamentoTipo.Despesa, cat, conta.Id));
            await db.SaveChangesAsync();
            var service = new FluxoService(db);

            var card = await service.ObterCardAsync(hoje.Year, hoje.Month);

            Assert.Equal(500m, card.SaldoAnterior);
            Assert.Equal(120m, card.SaldoMes);
            Assert.Equal(620m, card.SaldoAcumulado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
