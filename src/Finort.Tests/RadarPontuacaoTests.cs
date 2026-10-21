using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class RadarPontuacaoTests
{
    /// <summary>Consumo do mês, média da base e pontos esperados do componente de consumo.</summary>
    public static TheoryData<decimal, decimal, int> ConsumoPorVariacao => new()
    {
        { 11000m, 10000m, -12 },  // +10% de variação — satura
        { 10500m, 10000m, -6 },   // +5% — metade do peso
        { 10000m, 10000m, 0 },    // estável — sem linha
        { 9500m, 10000m, 6 },     // -5% — metade do peso
        { 9000m, 10000m, 12 },    // -10% — satura
    };

    /// <summary>Receitas do mês, média da base e pontos esperados do componente de receitas.</summary>
    public static TheoryData<decimal, decimal, int> ReceitasPorVariacao => new()
    {
        { 11000m, 10000m, 10 },
        { 10500m, 10000m, 5 },
        { 10000m, 10000m, 0 },
        { 9500m, 10000m, -5 },
        { 9000m, 10000m, -10 },
    };

    /// <summary>Consumo, receitas e pontos esperados do componente de poupança (design §18).</summary>
    public static TheoryData<decimal, decimal, int> PoupancaPorTaxa => new()
    {
        { 8000m, 8000m, -12 },  // poupança 0% — pior caso
        { 9000m, 8000m, -12 },  // poupança negativa — pior caso
        { 7600m, 8000m, -6 },   // poupança 5%
        { 7200m, 8000m, 0 },    // poupança 10% — neutra
        { 6400m, 8000m, 6 },    // poupança 20%
        { 4000m, 8000m, 12 },   // poupança 50% — satura
    };

    /// <summary>Coeficiente de variação da série e pontos esperados do componente de estabilidade.</summary>
    public static TheoryData<decimal, int> EstabilidadePorCoeficiente => new()
    {
        { 0.20m, 5 },
        { 0.29m, 2 },
        { 0.35m, 0 },
        { 0.50m, -5 },
        { 0.90m, -5 },
    };

    private static RadarContexto Contexto(
        int mesesHistorico = 3,
        decimal consumoAtual = 5000m,
        decimal mediaConsumo = 5000m,
        decimal receitasAtual = 8000m,
        decimal mediaReceitas = 8000m,
        decimal investidoAtual = 0m,
        IReadOnlyList<decimal>? consumoMensais = null)
        => new RadarContexto(
            new DateOnly(2026, 10, 1), 20, true,
            mesesHistorico, 3,
            receitasAtual, consumoAtual,
            investidoAtual, investidoAtual,
            mediaConsumo, 0m, mediaReceitas,
            receitasAtual > 0m ? consumoAtual / receitasAtual : 0m,
            RadarCalculos.LimiarAbsoluto(consumoAtual),
            Array.Empty<RadarVariacaoCategoria>(),
            consumoMensais ?? new[] { 5000m, 5000m, 5000m });

    private static RadarInsight InsightCategoria(RadarNivel nivel) => new(
        nivel, "categoria_aumentou", "Alimentação aumentou", "texto",
        300m, 0.30m, 0, 0.25m, Guid.NewGuid(), null);

    private static RadarInsight InsightOutroTipo(RadarNivel nivel) => new(
        nivel, "saldo_negativo", "Saldo negativo", "texto",
        300m, 0.30m, 0, 0.25m, null, null);

    /// <summary>
    /// Série de 4 meses com coeficiente de variação exato: desvios ±k em torno da média,
    /// o que dá coeficiente = k/média. Os valores não precisam casar com o consumo do mês
    /// atual — o score só usa a série para medir a estabilidade.
    /// </summary>
    private static decimal[] SerieComCoeficiente(decimal coeficiente, decimal media = 10000m)
    {
        var k = media * coeficiente;
        return new[] { media + k, media + k, media - k, media - k };
    }

    /// <summary>Soma dos pontos das linhas cujo rótulo contém o trecho.</summary>
    private static int Pontos(RadarScore score, string trecho)
        => score.Componentes.Where(c => c.Rotulo.Contains(trecho)).Sum(c => c.Pontos);

    [Fact]
    public void Calcular_HistoricoInsuficiente_RetornaNulo()
    {
        var score = RadarPontuacao.Calcular(Contexto(mesesHistorico: 2), Array.Empty<RadarInsight>());

        Assert.Null(score);
    }

    [Fact]
    public void Calcular_SituacaoEquilibrada_RetornaScoreAcimaDaBase()
    {
        var score = RadarPontuacao.Calcular(Contexto(), Array.Empty<RadarInsight>());

        Assert.NotNull(score);
        Assert.InRange(score!.Valor, 60, 100);
        // Base 50 + 12 (poupança de 37,5%) + 5 (gastos estáveis).
        Assert.Equal(67, score.Valor);
        Assert.Equal("Boa", score.Faixa);
        // Só entram as linhas que efetivamente moveram o score.
        Assert.Equal(2, score.Componentes.Count);
        Assert.Contains(score.Componentes, c => c.Rotulo == "maior capacidade de poupança" && c.Pontos == 12);
        Assert.Contains(score.Componentes, c => c.Rotulo == "gastos estáveis" && c.Pontos == 5);
    }

    [Fact]
    public void Calcular_ConsumoAcimaDaMedia_ReduzOScore()
    {
        var referencia = RadarPontuacao.Calcular(Contexto(), Array.Empty<RadarInsight>())!;
        var score = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 6000m, mediaConsumo: 5000m), Array.Empty<RadarInsight>());

        Assert.True(score!.Valor < referencia.Valor);
        Assert.Equal(-12, Pontos(score, "consumo"));
        Assert.Contains(score.Componentes, c => c.Rotulo.Contains("consumo aumentou"));
    }

    [Fact]
    public void Calcular_ConsumoAbaixoDaMedia_AumentaOScore()
    {
        var referencia = RadarPontuacao.Calcular(Contexto(), Array.Empty<RadarInsight>())!;
        var score = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 4000m, mediaConsumo: 5000m), Array.Empty<RadarInsight>());

        Assert.True(score!.Valor > referencia.Valor);
        Assert.Equal(12, Pontos(score, "consumo"));
        Assert.Contains(score.Componentes, c => c.Rotulo.Contains("consumo diminuiu"));
    }

    [Fact]
    public void Calcular_RotuloDoComponenteDeConsumoNaoFalaDeDespesas()
    {
        // O score mede consumo: o rótulo da linha precisa dizer a mesma coisa que o card.
        var score = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 6000m, mediaConsumo: 5000m), Array.Empty<RadarInsight>())!;

        Assert.Contains(score.Componentes, c => c.Rotulo == "consumo aumentou");
        Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("despesa"));
    }

    [Fact]
    public void Calcular_PoupancaNaoCaiComOAporte()
    {
        // A poupança é medida sobre o consumo: o dinheiro investido não foi consumido.
        var semAporte = RadarPontuacao.Calcular(Contexto(), Array.Empty<RadarInsight>())!;
        var comAporte = RadarPontuacao.Calcular(
            Contexto(investidoAtual: 3000m), Array.Empty<RadarInsight>())!;

        Assert.Equal(Pontos(semAporte, "poupança"), Pontos(comAporte, "poupança"));
        Assert.True(comAporte.Valor >= semAporte.Valor);
    }

    [Fact]
    public void Calcular_ReceitasAcimaDaMedia_AumentaOScore()
    {
        var referencia = RadarPontuacao.Calcular(Contexto(), Array.Empty<RadarInsight>())!;
        var score = RadarPontuacao.Calcular(
            Contexto(receitasAtual: 9000m, mediaReceitas: 8000m), Array.Empty<RadarInsight>());

        Assert.True(score!.Valor > referencia.Valor);
        Assert.Equal(10, Pontos(score, "receitas"));
        Assert.Contains(score!.Componentes, c => c.Rotulo.Contains("receitas aumentaram"));
    }

    [Fact]
    public void Calcular_CategoriasForaDoPadrao_PenalizamOScore()
    {
        var semPenalidade = RadarPontuacao.Calcular(Contexto(), Array.Empty<RadarInsight>())!;
        var comPenalidade = RadarPontuacao.Calcular(
            Contexto(), new[] { InsightCategoria(RadarNivel.Critico), InsightCategoria(RadarNivel.Atencao) })!;

        Assert.True(comPenalidade.Valor < semPenalidade.Valor);
        Assert.Equal(-3, Pontos(comPenalidade, "categoria"));
    }

    [Fact]
    public void Calcular_HistoricoEstavel_AumentaOScore()
    {
        var estavel = RadarPontuacao.Calcular(
            Contexto(consumoMensais: SerieComCoeficiente(0.20m)), Array.Empty<RadarInsight>())!;
        var instavel = RadarPontuacao.Calcular(
            Contexto(consumoMensais: SerieComCoeficiente(0.50m)), Array.Empty<RadarInsight>())!;

        Assert.True(estavel.Valor > instavel.Valor);
        Assert.Equal(5, Pontos(estavel, "gastos"));
        Assert.Contains(estavel.Componentes, c => c.Rotulo.Contains("gastos estáveis"));
    }

    [Fact]
    public void Calcular_HistoricoInstavel_ReduzOScore()
    {
        var estavel = RadarPontuacao.Calcular(
            Contexto(consumoMensais: SerieComCoeficiente(0.20m)), Array.Empty<RadarInsight>())!;
        var instavel = RadarPontuacao.Calcular(
            Contexto(consumoMensais: SerieComCoeficiente(0.50m)), Array.Empty<RadarInsight>())!;

        Assert.True(instavel.Valor < estavel.Valor);
        Assert.Equal(-5, Pontos(instavel, "gastos"));
        Assert.Contains(instavel.Componentes, c => c.Rotulo.Contains("gastos instáveis"));
    }

    [Fact]
    public void Calcular_ScoreSempreEntreZeroECem()
    {
        var pior = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 100000m, mediaConsumo: 100m, receitasAtual: 100m, mediaReceitas: 100000m,
                consumoMensais: SerieComCoeficiente(0.90m)),
            Enumerable.Range(0, 8).Select(_ => InsightCategoria(RadarNivel.Critico)).ToArray())!;

        // Soma bruta dos componentes: 50 - 12 - 10 - 12 - 15 - 5 = -4 (limitada em 0).
        Assert.InRange(pior.Valor, 0, 100);
        Assert.Equal(0, pior.Valor);
    }

    [Theory]
    [MemberData(nameof(ConsumoPorVariacao))]
    public void Calcular_Consumo_UsaOsPontosDaEspecificacao(decimal consumo, decimal media, int esperado)
    {
        var score = RadarPontuacao.Calcular(
            Contexto(consumoAtual: consumo, mediaConsumo: media), Array.Empty<RadarInsight>())!;

        Assert.Equal(esperado, Pontos(score, "consumo"));
        if (esperado == 0)
            Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("consumo"));
    }

    [Theory]
    [MemberData(nameof(ReceitasPorVariacao))]
    public void Calcular_Receitas_UsaOsPontosDaEspecificacao(decimal receitas, decimal media, int esperado)
    {
        var score = RadarPontuacao.Calcular(
            Contexto(receitasAtual: receitas, mediaReceitas: media), Array.Empty<RadarInsight>())!;

        Assert.Equal(esperado, Pontos(score, "receitas"));
        if (esperado == 0)
            Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("receitas"));
    }

    [Theory]
    [MemberData(nameof(PoupancaPorTaxa))]
    public void Calcular_Poupanca_UsaOMapeamentoDaEspecificacao(decimal consumo, decimal receitas, int esperado)
    {
        var score = RadarPontuacao.Calcular(
            Contexto(consumoAtual: consumo, mediaConsumo: consumo,
                receitasAtual: receitas, mediaReceitas: receitas),
            Array.Empty<RadarInsight>())!;

        Assert.Equal(esperado, Pontos(score, "poupança"));
        if (esperado == 0)
            Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("poupança"));
    }

    [Theory]
    [MemberData(nameof(EstabilidadePorCoeficiente))]
    public void Calcular_Estabilidade_UsaOsPontosDaEspecificacao(decimal coeficiente, int esperado)
    {
        var score = RadarPontuacao.Calcular(
            Contexto(consumoMensais: SerieComCoeficiente(coeficiente)), Array.Empty<RadarInsight>())!;

        Assert.Equal(esperado, Pontos(score, "gastos"));
        if (esperado == 0)
            Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("gastos"));
    }

    [Fact]
    public void Calcular_Categorias_RespeitaOsPesosETeto()
    {
        var umCritico = RadarPontuacao.Calcular(Contexto(), new[] { InsightCategoria(RadarNivel.Critico) })!;
        var umaAtencao = RadarPontuacao.Calcular(Contexto(), new[] { InsightCategoria(RadarNivel.Atencao) })!;
        var cincoCriticos = RadarPontuacao.Calcular(Contexto(),
            Enumerable.Range(0, 5).Select(_ => InsightCategoria(RadarNivel.Critico)).ToArray())!;
        var oitoCriticos = RadarPontuacao.Calcular(Contexto(),
            Enumerable.Range(0, 8).Select(_ => InsightCategoria(RadarNivel.Critico)).ToArray())!;
        var vinteCriticos = RadarPontuacao.Calcular(Contexto(),
            Enumerable.Range(0, 20).Select(_ => InsightCategoria(RadarNivel.Critico)).ToArray())!;

        Assert.Equal(-2, Pontos(umCritico, "categoria"));
        Assert.Equal(-1, Pontos(umaAtencao, "categoria"));
        Assert.Equal(-10, Pontos(cincoCriticos, "categoria"));
        Assert.Equal(-15, Pontos(oitoCriticos, "categoria"));
        Assert.Equal(-15, Pontos(vinteCriticos, "categoria"));
    }

    [Fact]
    public void Calcular_InsightDeOutroTipo_NaoPenaliza()
    {
        var semInsight = RadarPontuacao.Calcular(Contexto(), Array.Empty<RadarInsight>())!;
        var comInsight = RadarPontuacao.Calcular(Contexto(), new[] { InsightOutroTipo(RadarNivel.Critico) })!;

        Assert.Equal(semInsight.Valor, comInsight.Valor);
        Assert.DoesNotContain(comInsight.Componentes, c => c.Rotulo.Contains("categoria"));
    }

    [Fact]
    public void Calcular_SemReceitasNoMes_OmiteOComponenteDePoupanca()
    {
        // Mês parcial sem receita: a taxa de poupança é indefinida, então não há o que pontuar.
        var score = RadarPontuacao.Calcular(
            Contexto(receitasAtual: 0m, mediaReceitas: 8000m), Array.Empty<RadarInsight>())!;

        Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("poupança"));
        Assert.Equal(0, Pontos(score, "poupança"));
        // A receita ausente já é punishida uma única vez, por "receitas diminuíram".
        Assert.Equal(-10, Pontos(score, "receitas"));
        // Base 50 - 10 (receitas) + 5 (gastos estáveis) — sem os -12 de poupança.
        Assert.Equal(45, score.Valor);
    }

    [Fact]
    public void Calcular_ComponentesComZeroPontos_NaoAparecemNaComposicao()
    {
        // Consumo e receitas na média, poupança de 10% (neutra) e coeficiente de 35% (neutro).
        var score = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 9000m, mediaConsumo: 9000m, receitasAtual: 10000m, mediaReceitas: 10000m,
                consumoMensais: SerieComCoeficiente(0.35m)),
            Array.Empty<RadarInsight>())!;

        Assert.Equal(50, score.Valor);
        Assert.Empty(score.Componentes);
    }

    [Fact]
    public void Calcular_ConsumoComMediaZero_NaoPontuaComOSentinela()
    {
        // A base não tem consumo para comparar: o 100% de VariacaoPercentual é o
        // sentinela da média zero, não uma medição. O componente de consumo se
        // abstém — não pontua e não gera linha na composição.
        var semConsumo = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 0m, mediaConsumo: 0m), Array.Empty<RadarInsight>())!;
        var comConsumo = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 5000m, mediaConsumo: 0m), Array.Empty<RadarInsight>())!;

        Assert.DoesNotContain(comConsumo.Componentes, c => c.Rotulo.Contains("consumo"));
        Assert.Equal(0, Pontos(comConsumo, "consumo"));
        // Nenhum ponto veio do sentinela: o score é o mesmo do contexto sem consumo.
        Assert.Equal(semConsumo.Valor, comConsumo.Valor);
    }

    [Fact]
    public void Calcular_ReceitasComMediaZero_NaoPontuaComOSentinela()
    {
        // Mesma coisa pelo lado das receitas: sem média na base não há o que comparar.
        // O consumo (4500 contra 5000) e a poupança (10%, que vale zero ponto) são
        // iguais nos dois contextos — a única diferença possível seria o sentinela.
        var semReceita = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 4500m, mediaConsumo: 5000m,
                receitasAtual: 0m, mediaReceitas: 0m), Array.Empty<RadarInsight>())!;
        var comReceita = RadarPontuacao.Calcular(
            Contexto(consumoAtual: 4500m, mediaConsumo: 5000m,
                receitasAtual: 5000m, mediaReceitas: 0m), Array.Empty<RadarInsight>())!;

        Assert.DoesNotContain(comReceita.Componentes, c => c.Rotulo.Contains("receitas"));
        Assert.Equal(0, Pontos(comReceita, "receitas"));
        // Nenhum ponto veio do sentinela: o score é o mesmo do contexto sem receita.
        Assert.Equal(semReceita.Valor, comReceita.Valor);
    }

    [Fact]
    public void Calcular_ComponentesQueSeAbstem_AComposicaoContinuaFechando()
    {
        // Consumo e receitas se abstêm (as duas médias são zero): a base 50 mais os
        // pontos das linhas visíveis continua sendo o score exibido.
        var score = RadarPontuacao.Calcular(Contexto(
            consumoAtual: 4500m, mediaConsumo: 0m,
            receitasAtual: 5000m, mediaReceitas: 0m), Array.Empty<RadarInsight>())!;

        Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("consumo"));
        Assert.DoesNotContain(score.Componentes, c => c.Rotulo.Contains("receitas"));
        Assert.Equal(RadarPontuacao.Base + score.Componentes.Sum(c => c.Pontos), score.Valor);
    }

    [Fact]
    public void Calcular_Faixa_RespeitaOsLimites()
    {
        // 39 — Crítica | 40 — Atenção
        Assert.Equal(39, Pontuacao(10000, 10000, 10000, 10000, 0.32m).Valor);
        Assert.Equal("Crítica", Pontuacao(10000, 10000, 10000, 10000, 0.32m).Faixa);
        Assert.Equal(40, Pontuacao(10000, 10000, 10000, 10000, 0.29m).Valor);
        Assert.Equal("Atenção", Pontuacao(10000, 10000, 10000, 10000, 0.29m).Faixa);

        // 59 — Atenção | 60 — Boa
        Assert.Equal(59, Pontuacao(9000, 10000, 10000, 10000, 0.44m).Valor);
        Assert.Equal("Atenção", Pontuacao(9000, 10000, 10000, 10000, 0.44m).Faixa);
        Assert.Equal(60, Pontuacao(9000, 10000, 10000, 10000, 0.41m).Valor);
        Assert.Equal("Boa", Pontuacao(9000, 10000, 10000, 10000, 0.41m).Faixa);

        // 79 — Boa | 80 — Excelente
        Assert.Equal(79, Pontuacao(9000, 10000, 11000, 10000, 0.29m).Valor);
        Assert.Equal("Boa", Pontuacao(9000, 10000, 11000, 10000, 0.29m).Faixa);
        Assert.Equal(80, Pontuacao(9000, 10000, 11000, 10000, 0.26m).Valor);
        Assert.Equal("Excelente", Pontuacao(9000, 10000, 11000, 10000, 0.26m).Faixa);
    }

    private static RadarScore Pontuacao(
        decimal consumo, decimal mediaConsumo, decimal receitas, decimal mediaReceitas, decimal coeficiente)
        => RadarPontuacao.Calcular(
            Contexto(consumoAtual: consumo, mediaConsumo: mediaConsumo,
                receitasAtual: receitas, mediaReceitas: mediaReceitas,
                consumoMensais: SerieComCoeficiente(coeficiente)),
            Array.Empty<RadarInsight>())!;
}
