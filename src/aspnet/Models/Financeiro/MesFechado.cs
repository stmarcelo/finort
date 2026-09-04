namespace Finort.Models.Financeiro;

public class MesFechado
{
    public Guid Id { get; set; }

    public int Mes { get; set; }
    public int Ano { get; set; }

    public DateTime DataFechamento { get; set; }

    public decimal SaldoAcumulado { get; set; }
}
