namespace Finort.Tests;

/// <summary>
/// Regras de tela da conferência de fatura. O conteúdo vem do .razor do projeto web —
/// a mesma técnica de <c>TestWebHostEnvironment</c>.
/// </summary>
public class FaturaConferenciaTextoTests
{
    private static string FonteDoWebApp(params string[] caminho)
    {
        var raiz = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "aspnet"));
        return File.ReadAllText(Path.Combine(new[] { raiz }.Concat(caminho).ToArray()));
    }

    [Fact]
    public void Fatura_AvancaParaOMesSeguinteQuandoAFaturaEstaFechada()
    {
        var pagina = FonteDoWebApp("Components", "Pages", "FaturaConferencia.razor");

        Assert.Contains("EhFechadaAsync", pagina);
        Assert.Contains("MesInicialVemDaUrl", pagina);
    }

    [Fact]
    public void Fatura_MostraACorDaPessoaDoCadastro()
    {
        var pagina = FonteDoWebApp("Components", "Pages", "FaturaConferencia.razor");

        Assert.Contains("CorDeExibicao", pagina);
    }

    [Fact]
    public void Fatura_AvancoTemLimiteDeMeses()
    {
        // Um cartão com todos os meses fechados há anos não pode gerar laço infinito:
        // o avanço para no máximo 24 meses.
        var pagina = FonteDoWebApp("Components", "Pages", "FaturaConferencia.razor");

        Assert.Contains("i < 24", pagina);
    }
}
