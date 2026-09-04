using Finort.Components;
using Finort.Models.Financeiro;

namespace Finort.Tests;

public class ProjetoSelectLogicTests
{
    private static readonly Guid PessoaA = Guid.NewGuid();
    private static readonly Guid PessoaB = Guid.NewGuid();

    private static List<Projeto> Projetos() =>
    [
        new() { Id = Guid.NewGuid(), Descricao = "Alpha", DataContratacao = new DateOnly(2024, 1, 1), PessoaId = PessoaA },
        new() { Id = Guid.NewGuid(), Descricao = "Beta", DataContratacao = new DateOnly(2026, 3, 1), PessoaId = PessoaA },
        new() { Id = Guid.NewGuid(), Descricao = "Outro", DataContratacao = new DateOnly(2026, 4, 1), PessoaId = PessoaB }
    ];

    [Fact]
    public void Filtrar_SemTexto_TodosDaPessoaOrdenadosDesc()
    {
        var r = ProjetoSelecaoLogica.Filtrar(Projetos().Where(p => p.PessoaId == PessoaA).ToList(), "");
        Assert.Equal(["Beta", "Alpha"], r.Select(p => p.Descricao));
    }

    [Fact]
    public void Filtrar_ComTexto_ApenasCorrespondentes()
    {
        var r = ProjetoSelecaoLogica.Filtrar(Projetos(), "alp");
        Assert.Equal(["Alpha"], r.Select(p => p.Descricao));
    }

    [Fact]
    public void PreSelecao_UnicoProjetoRetornaId_MultiplosRetornaNull()
    {
        var projetos = Projetos().Where(p => p.PessoaId == PessoaB).ToList();
        Assert.NotNull(ProjetoSelecaoLogica.PreSelecaoUnica(1, projetos));
        Assert.Null(ProjetoSelecaoLogica.PreSelecaoUnica(2, Projetos()));
    }
}
