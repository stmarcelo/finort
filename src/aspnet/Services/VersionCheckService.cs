using System.Net.Http.Json;
using Finort.Models;

namespace Finort.Services;

public class VersionCheckService
{
    private readonly HttpClient _http;
    private const string GitHubApiUrl = "https://api.github.com/repos/stmarcelo/finort/releases/latest";

    public VersionCheckService(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Finort/1.0");
    }

    public async Task<AppVersion> VerificarAsync()
    {
        var versaoAtual = ObterVersaoAtual();

        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);

            if (release is null)
                return CriarResultadoErro(versaoAtual);

            var publicadoEm = DateTime.Parse(release.published_at);
            var tagLimpa = release.tag_name.Replace("v", "");
            var novaDisponivel = CompararVersoes(tagLimpa, versaoAtual) > 0;

            return new AppVersion
            {
                TagName = tagLimpa,
                PublishedAt = publicadoEm,
                HtmlUrl = release.html_url,
                NovaVersaoDisponivel = novaDisponivel,
                VersaoAtual = versaoAtual
            };
        }
        catch
        {
            return CriarResultadoErro(versaoAtual);
        }
    }

    private static string ObterVersaoAtual()
    {
        var assembly = typeof(VersionCheckService).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "1.0.0";
    }

    internal static int CompararVersoesPublic(string v1, string v2) => CompararVersoes(v1, v2);

    private static int CompararVersoes(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(int.Parse).ToArray();
        var parts2 = v2.Split('.').Select(int.Parse).ToArray();

        for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;
            if (p1 != p2) return p1.CompareTo(p2);
        }
        return 0;
    }

    private static AppVersion CriarResultadoErro(string versaoAtual) => new()
    {
        VersaoAtual = versaoAtual,
        NovaVersaoDisponivel = false
    };

    private class GitHubRelease
    {
        public string tag_name { get; set; } = "";
        public string published_at { get; set; } = "";
        public string html_url { get; set; } = "";
    }
}
