using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public class Subcategoria
{
    public Guid Id { get; set; }

    [Required]
    public string Nome { get; set; } = string.Empty;

    public bool IsProtected { get; set; }

    public Guid CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
}
