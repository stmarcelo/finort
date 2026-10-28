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

    /// <summary>Última fatura paga do cartão; null quando nenhuma foi paga.</summary>
    public FaturaSituacao? UltimaFaturaPaga { get; set; }

    /// <summary>Fatura seguinte à última paga; null quando não há fatura paga.</summary>
    public FaturaCardLinha? ProximaFatura { get; set; }
}

/// <summary>Linha de fatura exibida no cartão: fechada (registro em Faturas) ou aberta (soma de lançamentos do mês).</summary>
public sealed record FaturaCardLinha(int AnoReferencia, int MesReferencia, decimal Valor, bool Fechada);
