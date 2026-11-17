namespace Finort.Tests;

/// <summary>
/// Ligações de texto das telas novas: rota, filtros e entrada no menu. A mesma técnica
/// dos testes de tela do Radar — lê o .razor do projeto web a partir da saída dos testes.
/// </summary>
public class RelatorioCategoriaTelasTests
{
    private static string FonteDoWebApp(params string[] caminho)
    {
        var raiz = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "aspnet"));
        return File.ReadAllText(Path.Combine(new[] { raiz }.Concat(caminho).ToArray()));
    }

    [Fact]
    public void Pagina_TemRotaFiltrosETabelaEsperada()
    {
        var pagina = FonteDoWebApp("Components", "Pages", "RelatorioDespesaReceitaCategoria.razor");

        Assert.Contains("@page \"/relatorios/despesa-receita-por-categoria\"", pagina);
        Assert.Contains("CategoriaSelect", pagina);
        Assert.Contains("SubcategoriaSelect", pagina);
        Assert.Contains("Total geral", pagina);
        Assert.Contains("Média mensal", pagina);
        Assert.Contains("<MudTh>Mês</MudTh>", pagina);
        Assert.Contains("<MudTh>Pessoa</MudTh>", pagina);
        Assert.Contains("<MudTh Style=\"text-align:right\">Total</MudTh>", pagina);
        Assert.DoesNotContain("Gerar PDF", pagina);
    }

    [Fact]
    public void Menu_Relatorios_TemNovoLinkEAcessoRapido()
    {
        var layout = FonteDoWebApp("Components", "Layout", "MainLayout.razor");

        Assert.Contains("Href=\"/relatorios/despesa-receita-por-categoria\"", layout);
        Assert.Contains("(\"Despesa/Receita por Categoria\", \"/relatorios/despesa-receita-por-categoria\")", layout);
    }

    [Fact]
    public void DialogProjeto_TemCampoPrazoOpcional()
    {
        var dialog = FonteDoWebApp("Components", "Dialogs", "ProjetoDialog.razor");

        Assert.Contains("Prazo (opcional)", dialog);
        Assert.Contains("_prazo", dialog);
    }
}
