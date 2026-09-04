using Finort.Services;

namespace Finort.Tests;

public class AppUpdateServiceTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]   // não-Windows (Docker/Linux)
    [InlineData(true, false, false)]   // sistema atualizado
    [InlineData(false, false, false)]
    public void DeveMostrarBotao_MatrizDeDecisao(bool ehWindows, bool novaVersao, bool esperado)
    {
        Assert.Equal(esperado,
            AppUpdateService.DeveMostrarBotao(ehWindows, novaVersao));
    }

    [Theory]
    [InlineData("https://github.com/stmarcelo/finort/releases/tag/v0.1.1", "https://github.com/stmarcelo/finort/releases/tag/v0.1.1", "0.1.2")]
    public void ResolverUrlRelease_HtmlUrlPresente_TemPrecedencia(string esperado, string htmlUrl, string tagName)
    {
        Assert.Equal(esperado, AppUpdateService.ResolverUrlRelease(htmlUrl, tagName));
    }

    [Fact]
    public void ResolverUrlRelease_SemHtmlUrl_UsaTagConhecida()
    {
        // Caminho do cache de 24 h: AppVersion vem do banco sem HtmlUrl.
        Assert.Equal("https://github.com/stmarcelo/finort/releases/tag/v0.1.1",
            AppUpdateService.ResolverUrlRelease("", "0.1.1"));
    }

    [Fact]
    public void ResolverUrlRelease_NadaConhecido_UsaPaginaDeReleases()
    {
        Assert.Equal("https://github.com/stmarcelo/finort/releases/latest",
            AppUpdateService.ResolverUrlRelease("", ""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://evil.com/finort/releases")]
    [InlineData("http://github.com/stmarcelo/finort/releases/tag/v0.1.1")]
    public void AbrirPaginaRelease_UrlForaDoRepositorioOficial_Lanca(string url)
    {
        Assert.Throws<ArgumentException>(() => new AppUpdateService().AbrirPaginaRelease(url));
    }

    [Fact]
    public void CaminhoUpdater_ApontaParaUpdaterNaBaseDoApp()
    {
        var service = new AppUpdateService();
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "updater.exe"),
            service.CaminhoUpdater());
    }

    [Fact]
    public void UpdaterDisponivel_SemUpdaterExe_RetornaFalso()
    {
        // O projeto Updater é referenciado pelos testes e copia o apphost (Updater.exe)
        // para o bin de testes; renomeia temporariamente para simular sua ausência.
        var service = new AppUpdateService();
        var updater = service.CaminhoUpdater();
        var renomeado = updater + ".teste-ausente";
        var existia = File.Exists(updater);
        if (existia)
            File.Move(updater, renomeado);
        try
        {
            Assert.False(service.UpdaterDisponivel());
        }
        finally
        {
            if (existia)
                File.Move(renomeado, updater);
        }
    }

    [Fact]
    public void IniciarAtualizacao_VersaoInvalida_Lanca()
    {
        Assert.Throws<ArgumentException>(() => new AppUpdateService().IniciarAtualizacao("abc;del"));
    }
}
