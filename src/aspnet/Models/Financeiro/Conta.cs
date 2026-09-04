using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public class Conta
{
    public Guid Id { get; set; }

    public string? Banco { get; set; }
    public string? Agencia { get; set; }
    public string? ContaEDigito { get; set; }

    [Required]
    public string Nome { get; set; } = string.Empty;
}
