using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class CategoriaService
{
    private readonly AppDbContext _db;

    public CategoriaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Categoria>> ListarAsync()
        => await _db.Categorias
            .Include(c => c.Subcategorias)
            .OrderBy(c => c.Nome)
            .ToListAsync();

    internal static string NormalizarNome(string nome)
    {
        var texto = nome.Trim();
        return texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
    }

    public async Task<Categoria> CriarCategoriaAsync(string nome)
    {
        var categoria = new Categoria { Nome = NormalizarNome(nome) };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();
        return categoria;
    }

    public async Task<Subcategoria> AdicionarSubcategoriaAsync(Guid categoriaId, string nome)
    {
        var subcategoria = new Subcategoria { CategoriaId = categoriaId, Nome = NormalizarNome(nome) };
        _db.Subcategorias.Add(subcategoria);
        await _db.SaveChangesAsync();
        return subcategoria;
    }

    public async Task AtualizarCategoriaAsync(Categoria categoria, string nome)
    {
        categoria.Nome = NormalizarNome(nome);
        await _db.SaveChangesAsync();
    }

    public async Task AtualizarSubcategoriaAsync(Subcategoria subcategoria, string nome)
    {
        subcategoria.Nome = NormalizarNome(nome);
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirCategoriaAsync(Guid id)
    {
        var categoria = await _db.Categorias.FindAsync(id)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        if (categoria.IsProtected)
            throw new InvalidOperationException("Esta categoria é protegida e não pode ser excluída.");

        if (await _db.Lancamentos.AnyAsync(l => l.CategoriaId == id))
            throw new InvalidOperationException("Esta categoria possui lançamentos vinculados e não pode ser excluída.");

        _db.Categorias.Remove(categoria);
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirSubcategoriaAsync(Guid id)
    {
        var subcategoria = await _db.Subcategorias.FindAsync(id)
            ?? throw new InvalidOperationException("Subcategoria não encontrada.");

        if (subcategoria.IsProtected)
            throw new InvalidOperationException("Esta subcategoria é protegida e não pode ser excluída.");

        if (await _db.Lancamentos.AnyAsync(l => l.SubcategoriaId == id))
            throw new InvalidOperationException("Esta subcategoria possui lançamentos vinculados e não pode ser excluída.");

        _db.Subcategorias.Remove(subcategoria);
        await _db.SaveChangesAsync();
    }
}
