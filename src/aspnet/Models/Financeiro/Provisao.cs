using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public enum ProvisaoOnde
{
    DebitoConta,
    DebitoCartao,
    Receita
}

public enum ProvisaoFrequencia
{
    Mensal,
    Trimestral,
    Semestral,
    Anual
}

public enum RecorrenciaFrequencia
{
    Mensal,
    Trimestral,
    Semestral,
    Anual
}

public class Provisao
{
    public Guid Id { get; set; }

    public ProvisaoOnde Onde { get; set; }
    public ProvisaoFrequencia Frequencia { get; set; }

    /// <summary>Dia do mês do lançamento (clampado ao último dia do mês).</summary>
    public int Dia { get; set; }

    public Guid? PessoaId { get; set; }
    public Pessoa? Pessoa { get; set; }

    /// <summary>Conta de destino (DébitoConta e Receita).</summary>
    public Guid? ContaId { get; set; }
    public Conta? Conta { get; set; }

    /// <summary>Cartão de destino (DébitoCartao).</summary>
    public Guid? CartaoCreditoId { get; set; }
    public CartaoCredito? CartaoCredito { get; set; }

    public decimal Valor { get; set; }

    /// <summary>Se true, alterações no lançamento não propagam para a provisão.</summary>
    public bool ValorVariante { get; set; }

    [Required]
    public Guid CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public Guid? SubcategoriaId { get; set; }
    public Subcategoria? Subcategoria { get; set; }

    /// <summary>Último mês/ano lançado pelo sincronismo.</summary>
    public int? UltimoMesLancado { get; set; }
    public int? UltimoAnoLancado { get; set; }
}
