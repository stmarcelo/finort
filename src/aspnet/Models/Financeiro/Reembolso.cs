namespace Finort.Models.Financeiro;
public class Reembolso
{
    public Guid Id { get; set; }
    public Guid PessoaId { get; set; }
    public Pessoa Pessoa { get; set; } = null!;
    public Guid CartaoCreditoId { get; set; }
    public CartaoCredito CartaoCredito { get; set; } = null!;
    public Guid LancamentoId { get; set; }
    public Lancamento Lancamento { get; set; } = null!;
    public int? ParcelaAtual { get; set; }
    public int? TotalParcelas { get; set; }
    public decimal Valor { get; set; }
    public DateOnly Vencimento { get; set; }
    public bool Fechado { get; set; }
    public DateTime? DataFechamento { get; set; }
    public Guid? ReceitaId { get; set; }
    public Lancamento? Receita { get; set; }
}
