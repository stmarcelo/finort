using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

/// <summary>
/// O Radar mede consumo, não saída de dinheiro. O aporte em investimento é uma boa
/// notícia: ele não vira despesa a ser criticada, não infla o comprometimento e não
/// derruba o score. O saldo, ao contrário, é o saldo real — com o aporte descontado.
/// </summary>
public class RadarInvestimentoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 20);

    private static (AppDbContext Db, string File, Categoria Salario, Categoria Mercado, Categoria Investimento) Setup()
    {
        var (db, file) = TestDbContext.Create();
        var salario = new Categoria { Nome = "Salário" };
        var mercado = new Categoria { Nome = "Mercado" };
        var investimento = new Categoria { Nome = RadarFiltro.NomeCategoriaInvestimento };
        db.Categorias.Add(salario);
        db.Categorias.Add(mercado);
        db.Categorias.Add(investimento);
        db.SaveChanges();
        return (db, file, salario, mercado, investimento);
    }

    private static Lancamento Lancamento(
        AppDbContext db, DateOnly data, LancamentoTipo tipo, decimal valor, Guid categoriaId)
    {
        var l = new Lancamento
        {
            Data = data,
            Tipo = tipo,
            Valor = tipo == LancamentoTipo.Despesa ? -Math.Abs(valor) : Math.Abs(valor),
            CategoriaId = categoriaId,
            Confirmado = true
        };
        db.Lancamentos.Add(l);
        db.SaveChanges();
        return l;
    }

    /// <summary>
    /// Receita de 5.000 e consumo de 1.000 em cada mês da base (jul/ago/set) e no mês
    /// atual (outubro). O aporte é opcional e vai na categoria "Investimento" — é assim
    /// que o app registra aporte e compra (InvestimentoService.cs:378).
    /// </summary>
    private static (AppDbContext Db, string File, Categoria Salario, Categoria Mercado, Categoria Investimento)
        MontarCenario(decimal aporteBase, decimal aporteAtual)
    {
        var (db, file, salario, mercado, investimento) = Setup();
        for (var m = 7; m <= 9; m++)
        {
            Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Receita, 5000m, salario.Id);
            Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 1000m, mercado.Id);
            if (aporteBase > 0m)
                Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, aporteBase, investimento.Id);
        }
        Lancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Receita, 5000m, salario.Id);
        Lancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 1000m, mercado.Id);
        if (aporteAtual > 0m)
            Lancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, aporteAtual, investimento.Id);
        return (db, file, salario, mercado, investimento);
    }

    /// <summary>
    /// Mesmo cenário de <see cref="MontarCenario"/>, mas com o aporte em <b>um</b> mês da
    /// base (agosto) e nada nos outros: é o aporte irregular, que não se repete.
    /// </summary>
    private static (AppDbContext Db, string File, Categoria Salario, Categoria Mercado, Categoria Investimento)
        MontarCenarioAporteIrregular(decimal aporte, int mesDoAporte)
    {
        var (db, file, salario, mercado, investimento) = Setup();
        for (var m = 7; m <= 9; m++)
        {
            Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Receita, 5000m, salario.Id);
            Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 1000m, mercado.Id);
            if (m == mesDoAporte)
                Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, aporte, investimento.Id);
        }
        Lancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Receita, 5000m, salario.Id);
        Lancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 1000m, mercado.Id);
        return (db, file, salario, mercado, investimento);
    }

    [Fact]
    public async Task ObterAsync_AporteNaoEntraNoConsumoNemViraCategoriaDoRadar()
    {
        var (db, file, _, _, investimento) = MontarCenario(500m, 900m);
        try
        {
            var service = new RadarService(db);

            var r = await service.ObterAsync(3, null, Hoje);

            // O consumo é só o que foi de fato consumido: o aporte não entra.
            Assert.Equal(1000m, r.TotalConsumo);
            // A categoria "Investimento" não é consumo: não aparece em "Principais mudanças".
            Assert.DoesNotContain(r.Categorias, c => c.Nome == RadarFiltro.NomeCategoriaInvestimento);
            Assert.Equal("Mercado", Assert.Single(r.Categorias).Nome);
            // Nem vira insight de categoria (antes era "Investimento aumentou", que podia
            // ser crítico e acusar o usuário por ter investido).
            Assert.DoesNotContain(r.Insights, i => i.CategoriaId == investimento.Id);
            Assert.DoesNotContain(r.Insights, i => i.Tipo is "categoria_aumentou" or "categoria_diminuiu");
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_AporteEntraNoTotalInvestidoENaSerieMensal()
    {
        var (db, file, _, _, _) = MontarCenario(500m, 900m);
        try
        {
            var service = new RadarService(db);

            var r = await service.ObterAsync(3, null, Hoje);

            Assert.Equal(900m, r.TotalInvestido);
            Assert.Equal(900m, r.SerieMensal.Single(s => s.Ano == 2026 && s.Mes == 10).Investido);
            Assert.Equal(500m, r.SerieMensal.Single(s => s.Ano == 2026 && s.Mes == 9).Investido);
            // A série do gráfico é o consumo (sem o aporte) e o aporte tem série própria:
            // as séries "Consumo" e "Investido" não se sobrepõem.
            Assert.Equal(1000m, r.SerieMensal.Single(s => s.Ano == 2026 && s.Mes == 10).Consumo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_SaldoDescontaOAporteEComOAporteDiminui()
    {
        var (dbSem, fileSem, _, _, _) = MontarCenario(0m, 0m);
        var (dbCom, fileCom, _, _, _) = MontarCenario(0m, 900m);
        try
        {
            var semAporte = await new RadarService(dbSem).ObterAsync(3, null, Hoje);
            var comAporte = await new RadarService(dbCom).ObterAsync(3, null, Hoje);

            // O saldo é o saldo real: receitas − consumo − aporte.
            Assert.Equal(5000m - 1000m - 900m, comAporte.Saldo);
            // O aporte sai do dinheiro da conta: o saldo real diminui.
            Assert.Equal(900m, semAporte.Saldo - comAporte.Saldo);
        }
        finally
        {
            TestDbContext.Cleanup(dbSem, fileSem);
            TestDbContext.Cleanup(dbCom, fileCom);
        }
    }

    [Fact]
    public async Task ObterAsync_AporteNaoReduzOScore()
    {
        var (dbSem, fileSem, _, _, _) = MontarCenario(0m, 0m);
        var (dbCom, fileCom, _, _, _) = MontarCenario(500m, 900m);
        try
        {
            var semAporte = await new RadarService(dbSem).ObterAsync(3, null, Hoje);
            var comAporte = await new RadarService(dbCom).ObterAsync(3, null, Hoje);

            Assert.NotNull(semAporte.Score);
            Assert.NotNull(comAporte.Score);
            // Investir não pune: o score com aporte não pode ser menor que o score sem aporte.
            Assert.True(comAporte.Score!.Valor >= semAporte.Score!.Valor,
                $"Score com aporte ({comAporte.Score.Valor}) não pode ser menor que sem aporte ({semAporte.Score.Valor}).");
        }
        finally
        {
            TestDbContext.Cleanup(dbSem, fileSem);
            TestDbContext.Cleanup(dbCom, fileCom);
        }
    }

    [Fact]
    public async Task ObterAsync_AporteIrregularNaoReduzOScore()
    {
        // O score mede consumo. Um aporte de R$ 3.000 em um único mês fechado não pode
        // virar "gastos instáveis": o consumo (R$ 1.000 por mês) está perfeitamente estável.
        var (dbSem, fileSem, _, _, _) = MontarCenario(0m, 0m);
        var (dbCom, fileCom, _, _, _) = MontarCenarioAporteIrregular(3000m, 8);
        try
        {
            var semAporte = await new RadarService(dbSem).ObterAsync(3, null, Hoje);
            var comAporte = await new RadarService(dbCom).ObterAsync(3, null, Hoje);

            Assert.NotNull(semAporte.Score);
            Assert.NotNull(comAporte.Score);
            // Investir não pune: o score com aporte irregular não pode ser menor que o
            // score do cenário equivalente sem aporte.
            Assert.True(comAporte.Score!.Valor >= semAporte.Score!.Valor,
                $"Score com aporte irregular ({comAporte.Score.Valor}) não pode ser menor que sem aporte ({semAporte.Score.Valor}).");
        }
        finally
        {
            TestDbContext.Cleanup(dbSem, fileSem);
            TestDbContext.Cleanup(dbCom, fileCom);
        }
    }

    [Fact]
    public async Task ObterAsync_SerieMensalNaoMisturaConsumoComAporte()
    {
        // A série do gráfico mede consumo: o aporte tem série própria ("Investido").
        // As duas não podem se sobrepor — senão o usuário vê dois valores paralelos que
        // um contém o outro.
        var (db, file, _, _, _) = MontarCenario(500m, 900m);
        try
        {
            var service = new RadarService(db);

            var r = await service.ObterAsync(3, null, Hoje);

            // Mês atual: 1.000 de consumo e 900 de aporte — a série do consumo é 1.000.
            Assert.Equal(1000m, r.SerieMensal.Single(s => s.Ano == 2026 && s.Mes == 10).Consumo);
            // Mês fechado (setembro): 1.000 de consumo e 500 de aporte — a série do consumo é 1.000.
            Assert.Equal(1000m, r.SerieMensal.Single(s => s.Ano == 2026 && s.Mes == 9).Consumo);
            // E a série bate com o card "Consumo no mês".
            Assert.Equal(r.TotalConsumo, r.SerieMensal.Single(s => s.Ano == 2026 && s.Mes == 10).Consumo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_BaseSemConsumo_NaoCarregaOPercentualSentinela()
    {
        // A base (jul/ago/set) tem lançamentos, mas só aporte: o consumo da base é
        // R$ 0,00. Com R$ 500 de consumo no mês atual não há contra o que medir a
        // variação — o 100% de VariacaoPercentual é o sentinela da média zero, não uma
        // medição, e nem o card nem o insight podem mostrá-lo.
        var (db, file, _, mercado, investimento) = Setup();
        try
        {
            var service = new RadarService(db);
            for (var m = 7; m <= 9; m++)
                Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 1000m, investimento.Id);
            Lancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Despesa, 500m, mercado.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            // A base existe (3 meses com lançamentos), mas o consumo da base é zero.
            Assert.Equal(3, r.MesesBase);
            Assert.Equal(0m, r.MediaConsumo);
            Assert.Equal(500m, r.TotalConsumo);
            // O DTO não carrega o sentinela: o card não tem o que mostrar.
            Assert.Equal(0m, r.VariacaoPercentualConsumo);
            // E o insight diz o valor real, sem percentual inventado.
            var insight = Assert.Single(r.Insights, i => i.Tipo == "consumo_total_acima_da_media");
            Assert.DoesNotContain("%", insight.Descricao);
            Assert.Contains("R$ 500,00", insight.Descricao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_BaseSemReceita_NaoCarregaOPercentualSentinela()
    {
        // A base (jul/ago/set) tem lançamentos, mas só aporte: a receita da base é
        // R$ 0,00. Com R$ 500 de receita no mês atual não há contra o que medir a
        // variação — o 100% de VariacaoPercentual é o sentinela da média zero, não uma
        // medição, e nem o card nem o insight podem mostrá-lo.
        var (db, file, salario, _, investimento) = Setup();
        try
        {
            var service = new RadarService(db);
            for (var m = 7; m <= 9; m++)
                Lancamento(db, new DateOnly(2026, m, 10), LancamentoTipo.Despesa, 1000m, investimento.Id);
            Lancamento(db, new DateOnly(2026, 10, 10), LancamentoTipo.Receita, 500m, salario.Id);

            var r = await service.ObterAsync(3, null, Hoje);

            // A base existe (3 meses com lançamentos), mas a receita da base é zero.
            Assert.Equal(3, r.MesesBase);
            Assert.Equal(0m, r.MediaReceitas);
            Assert.Equal(500m, r.TotalReceitas);
            // O DTO não carrega o sentinela: o card não tem o que mostrar.
            Assert.Equal(0m, r.VariacaoPercentualReceitas);
            // E o insight diz o valor real, sem percentual inventado.
            var insight = Assert.Single(r.Insights, i => i.Tipo == "receita_total_acima_da_media");
            Assert.DoesNotContain("%", insight.Descricao);
            Assert.Contains("R$ 500,00", insight.Descricao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_AporteGrandeNaoElevaATaxaDeComprometimento()
    {
        var (db, file, _, _, _) = MontarCenario(0m, 4000m);
        try
        {
            var service = new RadarService(db);

            var r = await service.ObterAsync(3, null, Hoje);

            // O comprometimento mede o consumo sobre a receita: 1.000 de consumo sobre
            // 5.000 de receita. O aporte de 4.000 não entra na conta.
            Assert.Equal(0.2m, r.TaxaComprometimento);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
