namespace Finort.Models.Financeiro;

/// <summary>Viewmodel do card da página Investimentos.</summary>
public sealed record InvestimentoCard(
    Investimento Investimento,
    decimal SaldoReserva,
    decimal QuantidadeTotal,
    decimal Aportes = 0m,
    decimal Resgates = 0m,
    decimal Proventos = 0m,
    DateOnly? DataUltimoAporte = null,
    decimal ValorUltimoAporte = 0m)
{
    /// <summary>Saldo exibido: aportes + proventos − resgates para reserva/CDB; cotação × quantidade para os demais (dólar já soma rendimentos na quantidade).</summary>
    public decimal Saldo => Investimento.Tipo is TipoInvestimento.Reserva or TipoInvestimento.Cdb
        ? SaldoReserva
        : QuantidadeTotal * Investimento.ValorCotaAtual;
}

public sealed record InvestimentoTendenciaTipo(TipoInvestimento Tipo, List<decimal> Valores);
