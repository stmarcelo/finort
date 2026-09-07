namespace Finort.Models.Financeiro;

public class ContaResumo
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Banco { get; set; }

    /// <summary>Soma dos lançamentos confirmados.</summary>
    public decimal SaldoReal { get; set; }

    /// <summary>Soma de todos os lançamentos (confirmados + provisões).</summary>
    public decimal SaldoPrevisto { get; set; }

    /// <summary>Mês do último fechamento (null se nunca fechou).</summary>
    public int? UltimoMesFechado { get; set; }

    /// <summary>Ano do último fechamento (null se nunca fechou).</summary>
    public int? UltimoAnoFechado { get; set; }

    /// <summary>Saldo acumulado no último fechamento.</summary>
    public decimal? SaldoFechado { get; set; }
}
