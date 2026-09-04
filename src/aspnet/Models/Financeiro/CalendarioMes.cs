namespace Finort.Models.Financeiro;

/// <summary>Dados do calendário de compromissos de um mês.</summary>
public sealed record CalendarioMes(
    int Ano,
    int Mes,
    decimal TotalApagar,
    decimal TotalAreceber,
    decimal SaldoPrevisto,
    IReadOnlyList<CompromissoDia> Dias);

public sealed record CompromissoDia(DateOnly Data, IReadOnlyList<CompromissoItem> Itens);

/// <summary>
/// Compromisso exibido no calendário.
/// Valor assinado (despesa negativa). Projetada=true quando vem de provisão ainda não materializada.
/// </summary>
public sealed record CompromissoItem(
    decimal Valor,
    LancamentoTipo Tipo,
    bool Confirmado,
    string Descricao,
    string? Origem,
    bool Projetada,
    Guid? LancamentoId,
    bool Riscada = false,
    bool IsLembrete = false,
    bool IsFatura = false);
