namespace Finort.Models.Financeiro;

/// <summary>Compromisso de fatura para o calendário (mês de referência = mês exibido − 1).</summary>
public sealed record FaturaResumoCalendario(Guid CartaoId, DateOnly Vencimento, decimal ValorExibido, bool Paga);
