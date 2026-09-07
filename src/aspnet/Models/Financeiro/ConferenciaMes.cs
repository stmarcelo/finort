namespace Finort.Models.Financeiro;

/// <summary>Estado de conferência de um mês para a página Fechar mês.</summary>
public sealed record ConferenciaMes(
    Guid ContaId,
    string NomeConta,
    int Ano,
    int Mes,
    decimal SaldoAcumulado,
    bool TemPendencias,
    bool MesFechado);
