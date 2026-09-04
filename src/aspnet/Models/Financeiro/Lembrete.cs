using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public enum LembreteTipo
{
    Mensal,
    Unico
}

public class Lembrete
{
    public Guid Id { get; set; }

    [Required]
    public Guid PessoaId { get; set; }

    [Required]
    public LembreteTipo Tipo { get; set; }

    [Required]
    [MaxLength(100)]
    public string Texto { get; set; } = string.Empty;

    public int? Dia { get; set; }

    public DateOnly? Data { get; set; }

    public Pessoa? Pessoa { get; set; }
}
