namespace Finort.Models.Financeiro;

/// <summary>Pagamento registrado de uma fatura fechada (grupo por ReferenciaId).</summary>
public sealed record PagamentoFatura(
    Guid ReferenciaId,
    DateOnly DataPagamento,
    decimal ValorPago,
    string? ContaOrigem);

/// <summary>Situação consolidada de uma fatura fechada para o histórico.</summary>
public sealed record FaturaSituacao(
    Guid FaturaId,
    int AnoReferencia,
    int MesReferencia,
    decimal ValorTotal,
    decimal Pago,
    DateTime? DataFechamento)
{
    public bool Paga => Pago >= Math.Abs(ValorTotal);
    public bool Parcial => !Paga && Pago > 0m;
}
