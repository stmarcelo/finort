namespace Finort.Models.Financeiro;

/// <summary>Registro imutável gerado quando um investimento é excluído.</summary>
public class AuditoriaExclusaoInvestimento
{
    public Guid Id { get; set; }
    public string NomeInvestimento { get; set; } = string.Empty;
    public TipoInvestimento Tipo { get; set; }
    public decimal ValorCotaAtual { get; set; }
    public DateTime? DataCotacao { get; set; }
    public DateTime DataExclusao { get; set; }
}
