using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Financeiro;

public class Categoria
{
    public Guid Id { get; set; }

    [Required]
    public string Nome { get; set; } = string.Empty;

    public bool IsProtected { get; set; }

    public List<Subcategoria> Subcategorias { get; set; } = new();
}
