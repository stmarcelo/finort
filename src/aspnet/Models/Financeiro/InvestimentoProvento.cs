namespace Finort.Models.Financeiro;

public enum ProventoTipo
{
    Dividendo,
    Rendimento
}

/// <summary>Histórico de dividendos (não-reserva) e rendimentos (reserva).</summary>
public class InvestimentoProvento
{
    public Guid Id { get; set; }
    public Guid InvestimentoId { get; set; }
    public Investimento Investimento { get; set; } = null!;
    public DateOnly Data { get; set; }
    public decimal Valor { get; set; }
    public ProventoTipo Tipo { get; set; }
    public Guid? LancamentoId { get; set; }
    public Lancamento? Lancamento { get; set; }
}
