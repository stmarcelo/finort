using Finort.Services;

namespace Finort.Tests;

public class JanelaFluxoTests
{
    [Fact]
    public void DeMes_SemAntecipacao_RetornaOMesCheio()
    {
        var (inicio, fim) = JanelaFluxo.DeMes(new DateOnly(2026, 10, 1), 0);

        Assert.Equal(new DateOnly(2026, 10, 1), inicio);
        Assert.Equal(new DateOnly(2026, 10, 31), fim);
    }

    [Fact]
    public void DeMes_ComCincoDias_RetornaODiasDoMesSeguinte()
    {
        // A janela de outubro é 06/10..05/11 — a mesma do fluxo mensal.
        var (inicio, fim) = JanelaFluxo.DeMes(new DateOnly(2026, 10, 1), 5);

        Assert.Equal(new DateOnly(2026, 10, 6), inicio);
        Assert.Equal(new DateOnly(2026, 11, 5), fim);
    }

    [Fact]
    public void DeMes_ComAntecipacaoEmFevereiro_NaoPassaDoUltimoDia()
    {
        // Fevereiro tem 28 dias: a janela começa no dia 6 (5+1) e vai até 05/03.
        var (inicio, fim) = JanelaFluxo.DeMes(new DateOnly(2026, 2, 1), 5);

        Assert.Equal(new DateOnly(2026, 2, 6), inicio);
        Assert.Equal(new DateOnly(2026, 3, 5), fim);
    }

    [Fact]
    public void MesCorrente_NoMeioDoMes_EDoMesDoCalendario()
    {
        Assert.Equal(new DateOnly(2026, 10, 1),
            JanelaFluxo.MesCorrente(new DateOnly(2026, 10, 20), 5));
    }

    [Fact]
    public void MesCorrente_NosPrimeirosDias_EDoMesAnterior()
    {
        // Em 03/11 a janela de novembro (06/11..05/12) ainda não começou:
        // o mês corrente é outubro, cuja janela vai de 06/10 a 05/11.
        Assert.Equal(new DateOnly(2026, 10, 1),
            JanelaFluxo.MesCorrente(new DateOnly(2026, 11, 3), 5));
    }

    [Fact]
    public void MesCorrente_SemAntecipacao_EDoMesDoCalendario()
    {
        Assert.Equal(new DateOnly(2026, 10, 1),
            JanelaFluxo.MesCorrente(new DateOnly(2026, 10, 3), 0));
    }
}
