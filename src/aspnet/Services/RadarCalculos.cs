using Finort.Models.Financeiro;

namespace Finort.Services;

/// <summary>
/// Limiares e funções puras de cálculo do Radar Financeiro. Sem acesso a banco de dados:
/// tudo aqui é testável isoladamente (spec §11, §13, §31).
/// </summary>
public static class RadarCalculos
{
    /// <summary>Variação percentual mínima para considerar uma mudança relevante (spec §11).</summary>
    public const decimal VariacaoPercentualMinima = 0.10m;

    /// <summary>Piso absoluto (R$) de uma diferença relevante.</summary>
    public const decimal ValorAbsolutoMinimo = 20m;

    /// <summary>Participação mínima (fração do consumo do período) de uma diferença relevante.</summary>
    public const decimal ParticipacaoMinimaConsumo = 0.005m;

    /// <summary>Períodos mínimos para afirmar uma tendência (spec §13, §25).</summary>
    public const int MinimoMesesParaTendencia = 3;

    /// <summary>Máximo de insights exibidos (spec §20).</summary>
    public const int MaximoInsights = 8;

    /// <summary>Variação a partir da qual a mudança é crítica (spec §19).</summary>
    public const decimal VariacaoCritica = 0.25m;

    /// <summary>Taxa de comprometimento que exige atenção (spec §16).</summary>
    public const decimal ComprometimentoAtencao = 0.65m;

    /// <summary>Taxa de comprometimento crítica (spec §19).</summary>
    public const decimal ComprometimentoCritico = 0.80m;

    /// <summary>Variação total até a qual a série é considerada estável (spec §13).</summary>
    public const decimal VariacaoEstavelMinima = 0.05m;

    /// <summary>Limiar absoluto de uma variação: o maior entre o piso fixo e 0,5% do consumo do período.</summary>
    public static decimal LimiarAbsoluto(decimal totalConsumoPeriodo)
    {
        var porParticipacao = Math.Abs(totalConsumoPeriodo) * ParticipacaoMinimaConsumo;
        return Math.Max(ValorAbsolutoMinimo, porParticipacao);
    }

    /// <summary>
    /// Uma mudança é relevante quando variação percentual E diferença absoluta passam dos limiares.
    /// </summary>
    public static bool IsSignificantChange(decimal currentValue, decimal historicalAverage,
        decimal percentageThreshold, decimal absoluteThreshold)
    {
        var delta = currentValue - historicalAverage;
        if (Math.Abs(delta) < absoluteThreshold) return false;
        if (historicalAverage == 0m) return true;
        return Math.Abs(delta / historicalAverage) >= percentageThreshold;
    }

    /// <summary>
    /// Variação percentual em relação à média. Com média 0 a variação é tratada como
    /// infinita e representada por 100% (a relevância é decidida pelo limiar absoluto).
    /// </summary>
    public static decimal VariacaoPercentual(decimal atual, decimal media)
    {
        if (media != 0m) return (atual - media) / media;
        if (atual > 0m) return 1m;
        if (atual < 0m) return -1m;
        return 0m;
    }

    /// <summary>
    /// Tendência de uma série mensal. Exige ao menos 3 períodos; sequências oscilantes
    /// viram Indefinida (spec §13, §30).
    /// </summary>
    public static RadarTendencia DetectarTendencia(IReadOnlyList<decimal> valores)
    {
        if (valores.Count < MinimoMesesParaTendencia) return RadarTendencia.Indefinida;

        var crescente = true;
        var decrescente = true;
        for (var i = 1; i < valores.Count; i++)
        {
            if (valores[i] <= valores[i - 1]) crescente = false;
            if (valores[i] >= valores[i - 1]) decrescente = false;
        }

        var primeiro = valores[0];
        var ultimo = valores[^1];
        if (primeiro == 0m) return RadarTendencia.Indefinida;
        var variacaoTotal = (ultimo - primeiro) / primeiro;

        if (crescente)
            return Math.Abs(variacaoTotal) >= VariacaoEstavelMinima ? RadarTendencia.Alta : RadarTendencia.Estavel;
        if (decrescente)
            return Math.Abs(variacaoTotal) >= VariacaoEstavelMinima ? RadarTendencia.Baixa : RadarTendencia.Estavel;
        if (Math.Abs(variacaoTotal) <= VariacaoEstavelMinima) return RadarTendencia.Estavel;
        return RadarTendencia.Indefinida;
    }

    /// <summary>Média aritmética; 0 para entrada vazia.</summary>
    public static decimal Media(IEnumerable<decimal> valores)
    {
        var lista = valores.ToList();
        return lista.Count == 0 ? 0m : lista.Average();
    }

    /// <summary>Desvio padrão populacional; 0 para menos de dois valores.</summary>
    public static decimal DesvioPadrao(IEnumerable<decimal> valores)
    {
        var lista = valores.ToList();
        if (lista.Count < 2) return 0m;
        var media = lista.Average();
        var variancia = lista.Sum(v => (v - media) * (v - media)) / lista.Count;
        return (decimal)Math.Sqrt((double)variancia);
    }
}
