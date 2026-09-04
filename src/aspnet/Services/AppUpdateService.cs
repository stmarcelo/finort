using System.Diagnostics;

namespace Finort.Services;

/// <summary>
/// Atualização automática (Windows): decide a exibição do botão no Sobre e
/// lança o updater.exe (que faz backup do banco, instala e relança o app).
/// Sem updater.exe (ex.: rodando via dotnet run), o botão abre a página da
/// release no navegador como fallback.
/// </summary>
public class AppUpdateService
{
    private const string RepositorioOficial = "https://github.com/stmarcelo/finort";

    public static bool DeveMostrarBotao(bool ehWindows, bool novaVersaoDisponivel) =>
        ehWindows && novaVersaoDisponivel;

    /// <summary>HtmlUrl da API quando disponível; senão URL por tag; senão página de releases.</summary>
    public static string ResolverUrlRelease(string htmlUrl, string tagName)
    {
        if (!string.IsNullOrWhiteSpace(htmlUrl)) return htmlUrl;
        if (!string.IsNullOrWhiteSpace(tagName))
            return $"{RepositorioOficial}/releases/tag/v{tagName}";
        return $"{RepositorioOficial}/releases/latest";
    }

    public void AbrirPaginaRelease(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !url.StartsWith(RepositorioOficial, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("URL de release fora do repositório oficial.", nameof(url));

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    public string CaminhoUpdater() => Path.Combine(AppContext.BaseDirectory, "updater.exe");

    public bool UpdaterDisponivel() =>
        OperatingSystem.IsWindows() && File.Exists(CaminhoUpdater());

    public void IniciarAtualizacao(string versao)
    {
        if (string.IsNullOrWhiteSpace(versao) ||
            !System.Text.RegularExpressions.Regex.IsMatch(versao, @"^\d+(\.\d+)*$"))
            throw new ArgumentException("Versão inválida para atualização.", nameof(versao));

        var psi = new ProcessStartInfo
        {
            FileName = CaminhoUpdater(),
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--pid");
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        psi.ArgumentList.Add("--version");
        psi.ArgumentList.Add(versao);
        psi.ArgumentList.Add("--app-dir");
        psi.ArgumentList.Add(AppContext.BaseDirectory);
        Process.Start(psi);
    }
}
