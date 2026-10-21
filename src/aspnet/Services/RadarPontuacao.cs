using Finort.Models.Financeiro;

namespace Finort.Services;

/// <summary>
/// Score de saúde financeira de 0 a 100, aditivo e explicável (spec §18).
/// Cada componente vira uma linha visível na tela; componentes com zero pontos
/// não entram na composição. Componente sem base de comparação (média zero na base)
/// se abstém: não pontua e não vira linha.
/// </summary>
public static class RadarPontuacao
{
    public const int Base = 50;
    public const int PontosConsumo = 12;
    public const int PontosReceitas = 10;
    public const int PontosPoupanca = 12;
    public const int PontosEstabilidade = 5;
    public const int PontosCategoriaCritica = -2;
    public const int PontosCategoriaAtencao = -1;
    public const int TetoCategorias = -15;

    /// <summary>Variação que zera o componente de consumo/receitas (satura em ±100% do peso).</summary>
    public const decimal VariacaoMaxima = 0.10m;

    /// <summary>Poupança a partir da qual o componente de poupança satura em +12.</summary>
    public const decimal PoupancaMaxima = 0.30m;

    /// <summary>Poupança neutra: o componente de poupança vale zero (10% ⇒ 0 pontos).</summary>
    public const decimal PoupancaNeutra = 0.10m;

    /// <summary>Coeficiente de variação que dá nota máxima de estabilidade.</summary>
    public const decimal CoeficienteEstavel = 0.20m;

    /// <summary>Coeficiente de variação que dá nota mínima de estabilidade.</summary>
    public const decimal CoeficienteInstavel = 0.50m;

    /// <summary>
    /// Calcula o score. Devolve null quando o histórico é insuficiente (menos de 3 meses
    /// fechados com movimento na base de comparação).
    /// </summary>
    public static RadarScore? Calcular(RadarContexto ctx, IReadOnlyList<RadarInsight> insights)
    {
        if (ctx.MesesBaseComDados < RadarCalculos.MinimoMesesParaTendencia) return null;

        var componentes = new List<RadarScoreComponente>();

        // Sem média na base não há o que comparar: o 100% de VariacaoPercentual é o
        // sentinela da média zero, não uma medição. Um ponto tirado dele premia (ou
        // pune) o usuário por uma comparação que a própria tela diz que não pode ser
        // feita — e o valor não importaria, porque o sentinela é o mesmo para R$ 1 e
        // para R$ 10.000. O componente se abstém: não pontua e não entra na composição.
        if (ctx.MediaConsumo > 0m)
        {
            var variacaoConsumo = RadarCalculos.VariacaoPercentual(ctx.TotalConsumo, ctx.MediaConsumo);
            var pontosConsumo = Escalar(-variacaoConsumo, VariacaoMaxima, PontosConsumo);
            Adicionar(componentes, pontosConsumo, "consumo diminuiu", "consumo aumentou");
        }

        if (ctx.MediaReceitas > 0m)
        {
            var variacaoReceitas = RadarCalculos.VariacaoPercentual(ctx.TotalReceitas, ctx.MediaReceitas);
            var pontosReceitas = Escalar(variacaoReceitas, VariacaoMaxima, PontosReceitas);
            Adicionar(componentes, pontosReceitas, "receitas aumentaram", "receitas diminuíram");
        }

        // Sem receita no mês a taxa de poupança é indefinida: não há o que pontuar, e a
        // receita ausente já é tratada pelo componente de receitas.
        // A poupança é medida sobre o consumo: o dinheiro aplicado em investimento não foi
        // consumido, então investir não reduz a poupança — e não derruba o score.
        var poupanca = ctx.TotalReceitas > 0m
            ? (ctx.TotalReceitas - ctx.TotalConsumo) / ctx.TotalReceitas
            : (decimal?)null;
        if (poupanca.HasValue)
        {
            var pontosPoupanca = EscalarPoupanca(poupanca.Value);
            Adicionar(componentes, pontosPoupanca, "maior capacidade de poupança", "menor capacidade de poupança");
        }

        var categoriasCriticas = insights.Count(i => i.Tipo == "categoria_aumentou" && i.Nivel == RadarNivel.Critico);
        var categoriasAtencao = insights.Count(i => i.Tipo == "categoria_aumentou" && i.Nivel == RadarNivel.Atencao);
        var penalidade = categoriasCriticas * PontosCategoriaCritica + categoriasAtencao * PontosCategoriaAtencao;
        if (penalidade < TetoCategorias) penalidade = TetoCategorias;
        if (penalidade != 0)
            componentes.Add(new RadarScoreComponente(
                $"{categoriasCriticas + categoriasAtencao} categoria(s) acima do padrão", penalidade));

        // A estabilidade é medida sobre o consumo: o aporte em investimento não é gasto,
        // então não pode fazer o score dizer que os gastos estão instáveis.
        var mediaMensal = RadarCalculos.Media(ctx.ConsumoMensaisHistorico);
        if (mediaMensal > 0m && ctx.ConsumoMensaisHistorico.Count >= 2)
        {
            var coeficiente = RadarCalculos.DesvioPadrao(ctx.ConsumoMensaisHistorico) / mediaMensal;
            var amplitude = CoeficienteInstavel - CoeficienteEstavel;
            var razao = amplitude <= 0m ? 0m : (CoeficienteInstavel - coeficiente) / amplitude;
            var pontos = (int)Math.Round(PontosEstabilidade * 2m * razao - PontosEstabilidade, MidpointRounding.AwayFromZero);
            pontos = Math.Clamp(pontos, -PontosEstabilidade, PontosEstabilidade);
            Adicionar(componentes, pontos, "gastos estáveis", "gastos instáveis");
        }

        var total = Math.Clamp(Base + componentes.Sum(c => c.Pontos), 0, 100);
        return new RadarScore(total, Faixa(total), componentes);
    }

    /// <summary>
    /// Converte uma razão em pontos, saturando no peso máximo.
    /// </summary>
    private static int Escalar(decimal razao, decimal saturacao, int peso)
    {
        if (saturacao <= 0m) return 0;
        var pontos = (int)Math.Round(peso * razao / saturacao, MidpointRounding.AwayFromZero);
        return Math.Clamp(pontos, -peso, peso);
    }

    /// <summary>
    /// Mapeia a taxa de poupança para pontos (design §18): ≥ 30% ⇒ +12; ≤ 0% ⇒ −12;
    /// 10% ⇒ 0; interpolação linear entre os extremos. Só é chamada quando o mês tem
    /// receita — sem receita a taxa é indefinida e o componente não é emitido.
    /// </summary>
    private static int EscalarPoupanca(decimal poupanca)
    {
        if (poupanca >= PoupancaMaxima) return PontosPoupanca;
        if (poupanca <= 0m) return -PontosPoupanca;
        if (poupanca < PoupancaNeutra)
            return (int)Math.Round(-PontosPoupanca + PontosPoupanca * (poupanca / PoupancaNeutra),
                MidpointRounding.AwayFromZero);
        return (int)Math.Round(PontosPoupanca * ((poupanca - PoupancaNeutra) / (PoupancaMaxima - PoupancaNeutra)),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Adiciona a linha do componente — só entra na composição quando o componente
    /// efetivamente move o score.
    /// </summary>
    private static void Adicionar(List<RadarScoreComponente> componentes, int pontos, string rotuloPositivo, string rotuloNegativo)
    {
        if (pontos == 0) return;
        componentes.Add(new RadarScoreComponente(pontos > 0 ? rotuloPositivo : rotuloNegativo, pontos));
    }

    private static string Faixa(int valor) => valor switch
    {
        >= 80 => "Excelente",
        >= 60 => "Boa",
        >= 40 => "Atenção",
        _ => "Crítica"
    };
}
