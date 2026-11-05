using Finort.Models.Financeiro;

namespace Finort.Services;

/// <summary>
/// Janela do mês usada pelo fluxo mensal e pelo Radar Financeiro: a mesma.
/// <para>Com <c>D</c> dias de antecipação, o mês M vai de M/(D+1) até (M+1)/D —
/// cada lançamento pertence a um único mês. Sem antecipação (D = 0), a janela é o
/// mês cheio.</para>
/// </summary>
public static class JanelaFluxo
{
    public static (DateOnly Inicio, DateOnly Fim) DeMes(DateOnly mes, int diasAntecipacao)
    {
        var inicioMes = new DateOnly(mes.Year, mes.Month, 1);
        if (diasAntecipacao <= 0)
            return (inicioMes, inicioMes.AddMonths(1).AddDays(-1));

        var diasMes = DateTime.DaysInMonth(mes.Year, mes.Month);
        var proximo = inicioMes.AddMonths(1);
        var diasProximo = DateTime.DaysInMonth(proximo.Year, proximo.Month);
        return (new DateOnly(mes.Year, mes.Month, Math.Min(diasAntecipacao + 1, diasMes)),
                new DateOnly(proximo.Year, proximo.Month, Math.Min(diasAntecipacao, diasProximo)));
    }

    /// <summary>
    /// Mês corrente do fluxo: o mês cuja janela contém hoje. Nos primeiros D dias do
    /// mês a janela do mês de calendário ainda não começou — a que está em curso é a
    /// do mês anterior.
    /// </summary>
    public static DateOnly MesCorrente(DateOnly hoje, int diasAntecipacao)
    {
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1);
        return hoje.Day > diasAntecipacao ? inicio : inicio.AddMonths(-1);
    }
}
