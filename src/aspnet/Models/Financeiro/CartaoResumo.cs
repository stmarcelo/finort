namespace Finort.Models.Financeiro;

public class CartaoResumo
{
    public Guid Id { get; set; }
    public string Banco { get; set; } = string.Empty;
    public string Ultimos4Digitos { get; set; } = string.Empty;
    public int MelhorDiaCompra { get; set; }
    public int DiaVencimento { get; set; }
    public decimal Limite { get; set; }
    public decimal TotalNaoPago { get; set; }
    public decimal LimiteDisponivel => Limite + TotalNaoPago;
    public bool Ativo { get; set; }
    public Guid? ContaId { get; set; }
}
