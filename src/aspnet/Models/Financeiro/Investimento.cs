using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public enum TipoInvestimento
{
    Reserva,
    Dolar,
    Criptomoeda,
    Fii,
    Acao,
    Cdb
}

/// <summary>Investimento vinculado a uma conta bancária (reserva, dólar, cripto, fii, ação, cdb).</summary>
public class Investimento
{
    public Guid Id { get; set; }

    [Required]
    public string Nome { get; set; } = string.Empty;

    public TipoInvestimento Tipo { get; set; }

    /// <summary>Conta bancária onde dividendos são lançados e movimentações ocorrem.</summary>
    public Guid ContaVinculadaId { get; set; }
    public Conta Conta { get; set; } = null!;

    public string? Subtipo { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Cotação atual unitária; 0 quando ainda não informada.</summary>
    public decimal ValorCotaAtual { get; set; }

    /// <summary>Momento da última alteração da cotação.</summary>
    public DateTime? DataCotacao { get; set; }

    /// <summary>Data de vencimento do CDB.</summary>
    public DateTime? DataVencimento { get; set; }

    public bool Ativo { get; set; } = true;
}
