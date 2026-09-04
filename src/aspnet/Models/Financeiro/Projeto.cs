using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public class Projeto
{
    public Guid Id { get; set; }

    [Required]
    public string Descricao { get; set; } = string.Empty;

    public DateOnly DataContratacao { get; set; }
    public decimal ValorContratado { get; set; }

    public bool Concluido { get; set; }
    public DateOnly? DataConclusao { get; set; }

    public Guid PessoaId { get; set; }
    public Pessoa? Pessoa { get; set; }
}
