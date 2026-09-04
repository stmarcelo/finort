using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public class CartaoCredito
{
    public Guid Id { get; set; }

    [Required]
    public string Banco { get; set; } = string.Empty;

    [Required]
    public string Ultimos4Digitos { get; set; } = string.Empty;

    /// <summary>Dia que fecha a compra (1-31).</summary>
    public int MelhorDiaCompra { get; set; }

    /// <summary>Dia do vencimento da fatura (1-31).</summary>
    public int DiaVencimento { get; set; }

    public decimal Limite { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>Conta bancária usada para pagar a fatura (opcional).</summary>
    public Guid? ContaId { get; set; }
    public Conta? Conta { get; set; }
}
