using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public class Pessoa
{
    public Guid Id { get; set; }

    [Required]
    public string Nome { get; set; } = string.Empty;

    public string? CorDeExibicao { get; set; }
    public string? Observacao { get; set; }
}
