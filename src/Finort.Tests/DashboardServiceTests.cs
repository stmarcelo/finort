using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class DashboardServiceTests
{
    private static async Task<(AppDbContext Db, string File, Conta Conta, DashboardService Service)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Banco" };
        db.Contas.Add(conta);
        db.Categorias.Add(new Categoria { Nome = "Mercado" });
        await db.SaveChangesAsync();
        return (db, file, conta, new DashboardService(db, new CartaoCreditoService(db)));
    }

    // Adaptação: ObterAsync passou a receber o período (início/fim) em DateOnly
    // em vez de (ano, mês) inteiros; o helper reproduz o mês completo anterior.
    private static (DateOnly Inicio, DateOnly Fim) Mes(int ano, int mes)
        => (new DateOnly(ano, mes, 1), new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes)));

    private static async Task LanAsync(AppDbContext db, Guid contaId, DateOnly data,
        LancamentoTipo tipo, decimal valorAssinado, string categoriaNome, string? subcategoriaNome = null,
        string? pessoaNome = null)
    {
        var categoria = db.Categorias.First(c => c.Nome == categoriaNome);
        Subcategoria? sub = null;
        if (subcategoriaNome is not null)
        {
            sub = db.Subcategorias.First(s => s.CategoriaId == categoria.Id && s.Nome == subcategoriaNome);
        }

        Pessoa? pessoa = null;
        if (pessoaNome is not null)
        {
            pessoa = db.Pessoas.FirstOrDefault(p => p.Nome == pessoaNome);
            if (pessoa is null)
            {
                pessoa = new Pessoa { Nome = pessoaNome };
                db.Pessoas.Add(pessoa);
            }
        }

        db.Lancamentos.Add(new Lancamento
        {
            Data = data, Tipo = tipo, Valor = valorAssinado, ContaId = contaId,
            CategoriaId = categoria.Id, SubcategoriaId = sub?.Id, PessoaId = pessoa?.Id
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Obter_AgrupaDespesasEReceitasPorCategoria()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 5), LancamentoTipo.Despesa, -100m, "Mercado");
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 6), LancamentoTipo.Despesa, -50m, "Mercado");
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 7), LancamentoTipo.Despesa, -30m, "Contas de casa");
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 8), LancamentoTipo.Receita, 500m, "Receita");

            var (inicio, fim) = Mes(2026, 8);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Contains(dashboard.DespesasPorCategoria, c => c.Nome == "Mercado" && c.Valor == 150m);
            Assert.Contains(dashboard.DespesasPorCategoria, c => c.Nome == "Contas de casa" && c.Valor == 30m);
            Assert.Single(dashboard.ReceitasPorCategoria, c => c.Nome == "Receita" && c.Valor == 500m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_MesFuturo_IncluiProvisoesProjetadasNasCategorias()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var alvo = hoje.AddMonths(1);
            var mercado = db.Categorias.First(c => c.Nome == "Mercado");
            db.Provisoes.Add(new Provisao
            {
                Onde = ProvisaoOnde.DebitoConta, Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 10, Valor = 100m, CategoriaId = mercado.Id,
                UltimoMesLancado = hoje.Month, UltimoAnoLancado = hoje.Year
            });
            await db.SaveChangesAsync();

            var (inicio, fim) = Mes(alvo.Year, alvo.Month);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Contains(dashboard.DespesasPorCategoria, c => c.Nome == "Mercado" && c.Valor == 100m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_MesAtual_NaoIncluiProvisoes()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var mercado = db.Categorias.First(c => c.Nome == "Mercado");
            db.Provisoes.Add(new Provisao
            {
                Onde = ProvisaoOnde.DebitoConta, Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 10, Valor = 100m, CategoriaId = mercado.Id,
                UltimoMesLancado = hoje.Month, UltimoAnoLancado = hoje.Year
            });
            await db.SaveChangesAsync();

            var (inicio, fim) = Mes(hoje.Year, hoje.Month);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Empty(dashboard.DespesasPorCategoria);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_Top10Despesas_RetornaMaioresOrdenadas()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 5), LancamentoTipo.Despesa, -100m, "Mercado", null, "Ana");
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 6), LancamentoTipo.Despesa, -300m, "Contas de casa", "Energia", "Bruno");
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 7), LancamentoTipo.Despesa, -200m, "Mercado");

            var (inicio, fim) = Mes(2026, 8);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Equal(3, dashboard.TopDespesas.Count);
            Assert.Equal(300m, dashboard.TopDespesas[0].Valor);
            Assert.Equal("Contas de casa › Energia", dashboard.TopDespesas[0].Categoria);
            Assert.Equal("Bruno", dashboard.TopDespesas[0].Pessoa);
            // LancamentoTop não expõe mais a Data do lançamento; assertion removida na adaptação.
            Assert.Equal(200m, dashboard.TopDespesas[1].Valor);
            Assert.Null(dashboard.TopDespesas[1].Pessoa);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_Top10Receitas_RetornaMaioresOrdenadas()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 5), LancamentoTipo.Receita, 100m, "Receita", null, "Ana");
            await LanAsync(db, conta.Id, new DateOnly(2026, 8, 6), LancamentoTipo.Receita, 900m, "Receita", null, "Bruno");

            var (inicio, fim) = Mes(2026, 8);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Equal(2, dashboard.TopReceitas.Count);
            Assert.Equal(900m, dashboard.TopReceitas[0].Valor);
            Assert.Equal("Bruno", dashboard.TopReceitas[0].Pessoa);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_Patrimonios_CalculaAtivosEReserva()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimentoService = new InvestimentoService(db, new LancamentoService(db));
            var ativo = await investimentoService.CriarAsync("ITSA4", TipoInvestimento.Acao,
                conta.Id, null, null, 10m, DateTime.Today);
            await investimentoService.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 12m, null);
            var reserva = await investimentoService.CriarAsync("Reserva", TipoInvestimento.Reserva,
                conta.Id, null, null, null, null);
            await investimentoService.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 500m);
            await investimentoService.RegistrarProventoAsync(reserva.Id,
                new DateOnly(2026, 8, 10), 25m, ProventoTipo.Rendimento);

            var (inicio, fim) = Mes(2026, 8);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Contains(dashboard.Patrimonios, p => p.Nome == "ITSA4" && p.Valor == 120m);
            Assert.Contains(dashboard.Patrimonios, p => p.Nome == "Reserva" && p.Valor == 525m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_Patrimonios_MovimentoPopulaCotaEReserva()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimentoService = new InvestimentoService(db, new LancamentoService(db));
            var cota = await investimentoService.CriarAsync("CDB Banco", TipoInvestimento.Cdb,
                conta.Id, null, null, null, null);
            await investimentoService.RegistrarMovimentoAsync(cota.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 25m, null);
            var reserva = await investimentoService.CriarAsync("Reserva Emergencia", TipoInvestimento.Reserva,
                conta.Id, null, null, null, null);
            await investimentoService.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 6), MovimentoTipo.Aporte, null, null, 500m);

            var (inicio, fim) = Mes(2026, 8);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Contains(dashboard.Patrimonios, p => p.Nome == "CDB Banco" && p.Valor == 250m);
            Assert.Contains(dashboard.Patrimonios, p => p.Nome == "Reserva Emergencia" && p.Valor == 500m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_MesSemDados_ListasVazias()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var (inicio, fim) = Mes(2026, 8);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Empty(dashboard.DespesasPorCategoria);
            Assert.Empty(dashboard.ReceitasPorCategoria);
            Assert.Empty(dashboard.TopDespesas);
            Assert.Empty(dashboard.TopReceitas);
            Assert.Empty(dashboard.Patrimonios);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Obter_TopLimitaEmDez_QuandoHaMaisDeDez()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            // Top 10 agora agrupa por categoria/subcategoria/pessoa; pessoas distintas
            // mantêm 12 grupos para exercitar o limite de 10 (antes eram lançamentos individuais).
            for (var i = 1; i <= 12; i++)
                await LanAsync(db, conta.Id, new DateOnly(2026, 8, i), LancamentoTipo.Despesa, -(i * 10m), "Mercado", null, $"Pessoa{i:00}");

            var (inicio, fim) = Mes(2026, 8);
            var dashboard = await service.ObterAsync(inicio, fim);

            Assert.Equal(10, dashboard.TopDespesas.Count);
            Assert.Equal(120m, dashboard.TopDespesas[0].Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task EntradaDeCartao_AbateDespesaDaCategoria()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var cat = db.Categorias.First(c => c.Nome == "Alimentação");
            var cartao = new CartaoCredito { Banco = "T", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 1000m, Ativo = true };
            db.CartoesCredito.Add(cartao);
            db.Lancamentos.AddRange(
                new Lancamento { Data = new DateOnly(hoje.Year, hoje.Month, 3),
                    Tipo = LancamentoTipo.Despesa, Valor = -100m, CategoriaId = cat.Id,
                    CartaoCreditoId = cartao.Id },
                new Lancamento { Data = new DateOnly(hoje.Year, hoje.Month, 4),
                    Tipo = LancamentoTipo.Despesa, Valor = 40m, CategoriaId = cat.Id,
                    CartaoCreditoId = cartao.Id });
            await db.SaveChangesAsync();
            var service = new DashboardService(db, new CartaoCreditoService(db));

            var (inicio, fim) = Mes(hoje.Year, hoje.Month);
            var dados = await service.ObterAsync(inicio, fim);

            Assert.Equal(60m, dados.DespesasPorCategoria.Single(c => c.Nome == "Alimentação").Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CategoriaSohComEntrada_FicaZerada()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var cat = db.Categorias.First(c => c.Nome == "Alimentação");
            var cartao = new CartaoCredito { Banco = "T", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 1000m, Ativo = true };
            db.CartoesCredito.Add(cartao);
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(hoje.Year, hoje.Month, 4),
                Tipo = LancamentoTipo.Despesa, Valor = 40m, CategoriaId = cat.Id,
                CartaoCreditoId = cartao.Id
            });
            await db.SaveChangesAsync();
            var service = new DashboardService(db, new CartaoCreditoService(db));

            var (inicio, fim) = Mes(hoje.Year, hoje.Month);
            var dados = await service.ObterAsync(inicio, fim);

            Assert.DoesNotContain(dados.DespesasPorCategoria, c => c.Nome == "Alimentação");
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
