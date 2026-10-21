namespace Finort.Models.Financeiro;

/// <summary>Nível de severidade de um insight do Radar (spec §19).</summary>
public enum RadarNivel
{
    Positivo,
    Atencao,
    Critico
}

/// <summary>Tendência de uma série mensal (spec §13).</summary>
public enum RadarTendencia
{
    Alta,
    Baixa,
    Estavel,
    Indefinida
}

/// <summary>Receitas, consumo e aporte de investimento de um mês da série histórica.</summary>
/// <param name="Consumo">
/// Gasto de consumo do mês (sem o aporte em investimento) — a série do gráfico não
/// pode misturar consumo com aporte, senão as séries se sobrepõem.
/// </param>
/// <param name="Investido">Parte do dinheiro que foi aplicado em investimentos.</param>
public sealed record RadarSerieMensal(
    int Ano, int Mes, decimal Receitas, decimal Consumo, decimal Investido, bool Parcial);

/// <summary>Valor mensal de um recorte (ex.: histórico de uma categoria).</summary>
public sealed record RadarPontoMensal(int Ano, int Mes, decimal Valor, bool Parcial);

/// <summary>Variação de uma subcategoria dentro de uma categoria.</summary>
/// <param name="Id">Identificador da subcategoria.</param>
/// <param name="Nome">Nome da subcategoria.</param>
/// <param name="ValorAtual">Gasto da subcategoria no mês atual (janela truncada).</param>
/// <param name="MediaHistorica">Média da subcategoria na base de comparação.</param>
/// <param name="VariacaoAbsoluta">Diferença entre o mês atual e a média da subcategoria.</param>
/// <param name="VariacaoPercentual">Variação percentual do mês atual contra a média.</param>
/// <param name="ParticipacaoNaVariacao">
/// Fração do movimento bruto da categoria na direção da mudança (0..1). As partes somam 1 e
/// as subcategorias que se moveram na direção contrária recebem 0.
/// </param>
public sealed record RadarVariacaoSubcategoria(
    Guid Id, string Nome, decimal ValorAtual, decimal MediaHistorica,
    decimal VariacaoAbsoluta, decimal VariacaoPercentual, decimal ParticipacaoNaVariacao);

/// <summary>Variação de uma categoria entre o mês atual e a base histórica.</summary>
public sealed record RadarVariacaoCategoria(
    Guid Id, string Nome, decimal ValorAtual, decimal MediaHistorica,
    decimal VariacaoAbsoluta, decimal VariacaoPercentual, decimal Participacao,
    RadarTendencia Tendencia, int Persistencia,
    RadarVariacaoSubcategoria? SubcategoriaResponsavel);

/// <summary>Um fato relevante detectado pelo Radar.</summary>
public sealed record RadarInsight(
    RadarNivel Nivel, string Tipo, string Titulo, string Descricao,
    decimal Impacto, decimal Percentual, int Persistencia, decimal Participacao,
    Guid? CategoriaId, Guid? SubcategoriaId);

/// <summary>Uma linha da composição do score (ex.: "+8 receitas aumentaram").</summary>
public sealed record RadarScoreComponente(string Rotulo, int Pontos);

/// <summary>Score de saúde financeira, de 0 a 100, com a composição que o justifica.</summary>
public sealed record RadarScore(int Valor, string Faixa, IReadOnlyList<RadarScoreComponente> Componentes);

/// <summary>Detalhamento de uma categoria para o drill-down.</summary>
public sealed record RadarDetalheCategoria(
    Guid CategoriaId, string Nome, decimal ValorAtual, decimal MediaHistorica,
    decimal VariacaoPercentual, RadarTendencia Tendencia,
    IReadOnlyList<RadarPontoMensal> Historico,
    IReadOnlyList<RadarVariacaoSubcategoria> Subcategorias)
{
    /// <summary>
    /// Subcategoria responsável pela variação da categoria, escolhida pelo serviço.
    /// Segue o sentido da variação: na alta, a que mais subiu; na queda, a que mais caiu.
    /// Nula quando a categoria não variou — não há movimento a explicar.
    /// </summary>
    public RadarVariacaoSubcategoria? SubcategoriaResponsavel { get; init; }

    /// <summary>Número de meses que formam a base efetiva de comparação.</summary>
    public int MesesBase { get; init; }
}

/// <summary>
/// Entradas das análises (dados já filtrados, agregados e truncados pelo mês parcial).
/// <para><see cref="MesesBase"/> é a base efetiva de comparação: os meses fechados da base
/// que têm movimento dentro da janela truncada. É o número que forma as médias, o
/// denominador das categorias e o que aparece nas mensagens ("do último mês"). Zero
/// significa que não há base — nenhuma comparação é feita.</para>
/// <para><see cref="MesesBaseComDados"/> é o contador do portão do score: menos de 3 meses
/// com dados significa histórico insuficiente e não há score. Na prática vale o mesmo
/// número de <see cref="MesesBase"/>. Não confundir com
/// <see cref="RadarResultado.MesesHistorico"/>, que é o tamanho da janela pedida pelo
/// usuário.</para>
/// <para><see cref="TotalConsumo"/> é o gasto do mês sem o aporte em investimentos:
/// o Radar mede consumo, não saída de dinheiro. O aporte é uma boa notícia e vive em
/// <see cref="TotalInvestido"/>/<see cref="InvestidoAtual"/>, com a média de aportes da
/// base em <see cref="MediaInvestido"/>. O saldo, ao contrário, é o saldo real —
/// receitas menos consumo menos aporte.</para>
/// <para><see cref="ConsumoMensaisHistorico"/> é a série mensal do <b>consumo</b> (sem o
/// aporte): é ela que mede a estabilidade dos gastos. O aporte não entra — um aporte em
/// um mês fechado não pode fazer o consumo parecer instável.</para>
/// </summary>
public sealed record RadarContexto(
    DateOnly MesAtual, int DiaCorte, bool MesParcial,
    int MesesBaseComDados, int MesesBase,
    decimal TotalReceitas, decimal TotalConsumo, decimal TotalInvestido, decimal InvestidoAtual,
    decimal MediaConsumo, decimal MediaInvestido, decimal MediaReceitas, decimal TaxaComprometimento,
    decimal LimiarAbsoluto,
    IReadOnlyList<RadarVariacaoCategoria> Categorias,
    IReadOnlyList<decimal> ConsumoMensaisHistorico);

/// <summary>Resultado completo do Radar para a tela.</summary>
public sealed record RadarResultado(
    int MesesHistorico, DateOnly MesAtual, int DiaCorte, bool MesParcial,
    decimal TotalReceitas, decimal TotalConsumo, decimal TotalInvestido,
    decimal Saldo, decimal TaxaComprometimento,
    decimal VariacaoPercentualReceitas, decimal VariacaoPercentualConsumo,
    RadarScore? Score, IReadOnlyList<RadarInsight> Insights,
    IReadOnlyList<RadarVariacaoCategoria> Categorias,
    IReadOnlyList<RadarSerieMensal> SerieMensal)
{
    /// <summary>
    /// Número de meses que formam a base efetiva de comparação — os meses da base que
    /// têm lançamentos. É o número que aparece nas mensagens ("do último mês") e nos
    /// rótulos da tela. Zero significa que não há base: nenhuma comparação é feita.
    /// </summary>
    public int MesesBase { get; init; }

    /// <summary>
    /// Variação do aporte do mês contra a média de aportes da base. Zero quando não há
    /// base de comparação: não existe média contra a qual medir.
    /// </summary>
    public decimal VariacaoPercentualInvestido { get; init; }

    /// <summary>
    /// Média de aportes da base de comparação. Zero quando não há base: não existe
    /// média contra a qual medir o aporte do mês — e o 100% do VariacaoPercentual é o
    /// sentinela da média zero, não uma medição.
    /// </summary>
    public decimal MediaInvestido { get; init; }

    /// <summary>
    /// Média de consumo da base de comparação. Zero quando não há base de consumo: não
    /// existe média contra a qual medir o consumo do mês — e o 100% do VariacaoPercentual
    /// é o sentinela da média zero, não uma medição. É o que distingue "a base não tem
    /// consumo" de "o consumo do mês é igual à média".
    /// </summary>
    public decimal MediaConsumo { get; init; }

    /// <summary>
    /// Média de receitas da base de comparação. Zero quando não há base de receita: não
    /// existe média contra a qual medir as receitas do mês — e o 100% do VariacaoPercentual
    /// é o sentinela da média zero, não uma medição. É o que distingue "a base não tem
    /// receita" de "a receita do mês é igual à média".
    /// </summary>
    public decimal MediaReceitas { get; init; }
}
