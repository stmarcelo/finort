namespace Finort.Models.Financeiro;

/// <summary>Agregação categoria → valor magnitudepositiva para donuts.</summary>
public sealed record CategoriaValor(string Nome, decimal Valor);

/// <summary>Item agrupado do Top 10 (Valor sempre positivo/magnitude).</summary>
public sealed record LancamentoTop(string Categoria, string? Pessoa, decimal Valor);

/// <summary>Patrimônio atual de um investimento.</summary>
public sealed record InvestimentoPatrimonio(string Nome, decimal Valor);

/// <summary>Saldo de uma conta patrimonial.</summary>
public sealed record ContaPatrimonio(string Nome, string? Banco, decimal Valor);

/// <summary>Dados de um mês para tendência.</summary>
public sealed record MesTendencia(int Ano, int Mes, decimal Receitas, decimal Despesas);

/// <summary>Utilização de cartão de crédito.</summary>
public sealed record CartaoUtilizacao(string Nome, decimal Limite, decimal Utilizado, decimal Disponivel);

/// <summary>Agregados do período para a página Dashboard.</summary>
public sealed record DashboardMes(
    DateOnly PeriodoInicio,
    DateOnly PeriodoFim,
    IReadOnlyList<CategoriaValor> DespesasPorCategoria,
    IReadOnlyList<CategoriaValor> ReceitasPorCategoria,
    IReadOnlyList<LancamentoTop> TopDespesas,
    IReadOnlyList<LancamentoTop> TopReceitas,
    IReadOnlyList<InvestimentoPatrimonio> Patrimonios,
    IReadOnlyList<ContaPatrimonio> Contas,
    decimal TotalReceitas,
    decimal TotalDespesas,
    decimal TaxaPoupanca,
    IReadOnlyList<MesTendencia> TendenciaMensal,
    IReadOnlyList<CartaoUtilizacao> UtilizacaoCartoes);
