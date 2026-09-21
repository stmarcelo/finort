using Finort.Models.Financeiro;

namespace Finort.Tests;

public class InvestimentoResumoTests
{
    private static InvestimentoCard Card(TipoInvestimento tipo, decimal saldo)
    {
        var inv = new Investimento { Nome = "X", Tipo = tipo, ValorCotaAtual = 1m };
        return tipo is TipoInvestimento.Reserva or TipoInvestimento.Cdb
            ? new InvestimentoCard(inv, saldo, 0m)
            : new InvestimentoCard(inv, 0m, saldo); // qtd=saldo, cota=1 => Saldo=saldo
    }

    [Fact]
    public void Totais_SomaEPercentual()
    {
        var cards = new List<InvestimentoCard>
        {
            Card(TipoInvestimento.Acao, 700m),
            Card(TipoInvestimento.Fii, 300m),
        };
        var total = cards.Sum(c => c.Saldo);
        Assert.Equal(1000m, total);
        Assert.Equal(70m, Math.Round(cards.Where(c => c.Investimento.Tipo == TipoInvestimento.Acao).Sum(c => c.Saldo) / total * 100, 2));
    }

    [Fact]
    public void TotalZero_PercentualZero_SemDivisaoPorZero()
    {
        var cards = new List<InvestimentoCard>();
        var total = cards.Sum(c => c.Saldo);
        Assert.Equal(0m, total);
        decimal Pct(decimal v) => total == 0m ? 0m : v / total * 100;
        Assert.Equal(0m, Pct(0m));
    }
}
