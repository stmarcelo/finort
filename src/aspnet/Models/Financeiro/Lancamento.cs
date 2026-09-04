namespace Finort.Models.Financeiro;

public class Lancamento
{
    public Guid Id { get; set; }
    public DateOnly Data { get; set; }
    public LancamentoTipo Tipo { get; set; }

    /// <summary>Assinado: saídas (despesa e perna origem de transferência) negativas.</summary>
    public decimal Valor { get; set; }

    /// <summary>Nulo para despesas de cartão.</summary>
    public Guid? ContaId { get; set; }
    public Conta? Conta { get; set; }

    public Guid CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public Guid? SubcategoriaId { get; set; }
    public Subcategoria? Subcategoria { get; set; }

    public Guid? PessoaId { get; set; }
    public Pessoa? Pessoa { get; set; }

    public bool Confirmado { get; set; }

    /// <summary>Liga as 2 pernas de uma transferência.</summary>
    public Guid? ReferenciaId { get; set; }

    // ---- Fase 4a ----

    public Guid? CartaoCreditoId { get; set; }
    public CartaoCredito? CartaoCredito { get; set; }

    /// <summary>Vencimento da fatura do cartão ao qual este lançamento pertence.</summary>
    public DateOnly? DataVencimentoCartao { get; set; }

    /// <summary>Agrupa as parcelas de um mesmo parcelamento.</summary>
    public Guid? ParcelamentoId { get; set; }
    public int? ParcelaAtual { get; set; }
    public int? TotalParcelas { get; set; }

    /// <summary>Agrupa as repetições de uma recorrência.</summary>
    public Guid? RecorrenciaId { get; set; }

    /// <summary>No despesa: aponta para o lançamento de receita do reembolso.</summary>
    public Guid? ReembolsoId { get; set; }

    /// <summary>Preenchido quando o lançamento foi gerado por uma provisão.</summary>
    public Guid? ProvisaoId { get; set; }

    public Guid? ProjetoId { get; set; }
    public Projeto? Projeto { get; set; }
}
