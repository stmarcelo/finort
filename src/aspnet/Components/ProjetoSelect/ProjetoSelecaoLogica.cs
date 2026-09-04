using Finort.Models.Financeiro;

namespace Finort.Components;

public static class ProjetoSelecaoLogica
{
    public static IReadOnlyList<Projeto> Filtrar(IReadOnlyList<Projeto> projetosDaPessoa, string texto)
    {
        IEnumerable<Projeto> resultado = projetosDaPessoa;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var termo = texto.Trim();
            resultado = projetosDaPessoa.Where(p => p.Descricao.Contains(termo, StringComparison.OrdinalIgnoreCase));
        }
        return resultado.OrderByDescending(p => p.DataContratacao).ToList();
    }

    public static Guid? PreSelecaoUnica(int totalDaPessoa, IReadOnlyList<Projeto> projetosDaPessoa)
        => totalDaPessoa == 1 && projetosDaPessoa.Count == 1 ? projetosDaPessoa[0].Id : null;
}
