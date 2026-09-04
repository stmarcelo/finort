namespace Finort.Models.Financeiro;

public class Fatura
{
    public Guid Id { get; set; }

    public Guid CartaoCreditoId { get; set; }
    public CartaoCredito CartaoCredito { get; set; } = null!;

    public int AnoReferencia { get; set; }
    public int MesReferencia { get; set; }

    /// <summary>Somatório persistido no fechamento.</summary>
    public decimal ValorTotal { get; set; }

    public bool Fechada { get; set; }
    public DateTime? DataFechamento { get; set; }
}
