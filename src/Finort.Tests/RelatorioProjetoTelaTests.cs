namespace Finort.Tests;

/// <summary>A tela do relatório de projeto usa as mesmas linhas de cabeçalho do PDF.</summary>
public class RelatorioProjetoTelaTests
{
    private static string FonteDoWebApp(params string[] caminho)
    {
        var raiz = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "aspnet"));
        return File.ReadAllText(Path.Combine(new[] { raiz }.Concat(caminho).ToArray()));
    }

    [Fact]
    public void Tela_UsaLinhasDeCabecalhoCompartilhadasComOPdf()
    {
        var pagina = FonteDoWebApp("Components", "Pages", "RelatorioProjeto.razor");

        Assert.Contains("ProjetoRelatorioService.LinhasCabecalho", pagina);
        Assert.DoesNotContain("Contratação: @_relatorio.DataContratacao", pagina);
    }
}
