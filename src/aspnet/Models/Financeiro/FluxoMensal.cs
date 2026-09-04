namespace Finort.Models.Financeiro;

/// <summary>Total de despesas de um cartão no mês (rótulo "Banco ••4321").</summary>
public sealed record TotalCartao(Guid CartaoId, string Nome, decimal Total, bool Pago = false);

/// <summary>Agregados financeiros de um mês para a página Fluxo.</summary>
public sealed record FluxoMensal(
    int Ano,
    int Mes,
    decimal TotalReceitas,
    decimal TotalDespesas,
    IReadOnlyList<TotalCartao> TotaisPorCartao,
    decimal SaldoAnterior,
    decimal SaldoMes,
    decimal SaldoAcumulado,
    bool ReceitasPagas = false,
    bool DespesasPagas = false);
