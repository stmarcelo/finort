namespace Finort.Models.Financeiro;

public enum MovimentoTipo
{
    Compra,
    Venda,
    Aporte,
    Resgate
}

/// <summary>Movimentação de um investimento (compra/venda de ativos ou aporte/resgate de reserva).</summary>
public class InvestimentoMovimento
{
    public Guid Id { get; set; }
    public Guid InvestimentoId { get; set; }
    public Investimento Investimento { get; set; } = null!;
    public DateOnly Data { get; set; }
    public MovimentoTipo Tipo { get; set; }

    /// <summary>Nulo em aporte/resgate de reserva.</summary>
    public decimal? Quantidade { get; set; }

    /// <summary>Nulo em aporte/resgate de reserva.</summary>
    public decimal? ValorPorCota { get; set; }

    /// <summary>Valor total do movimento (qtd × cota, ou valor único na reserva).</summary>
    public decimal Valor { get; set; }

    /// <summary>Lançamento bancário gerado (link para estorno). Nulo quando o investimento é pré-existente.</summary>
    public Guid? LancamentoId { get; set; }
    public Lancamento? Lancamento { get; set; }
}
