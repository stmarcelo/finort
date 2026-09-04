using System.Net.Http.Json;
using System.Text.Json;

namespace Finort.Services;

public sealed class TurnstileVerifier(HttpClient http, TurnstileConfig config) : IPageVerifier
{
    private const string UrlSiteverify = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private sealed record SiteVerifyResponse(bool Success);

    public async Task<VerificationVerdict> VerificarAsync(string token, string ip, CancellationToken ct = default)
    {
        try
        {
            var resposta = await http.PostAsync(UrlSiteverify, new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["secret"] = config.SecretKey ?? string.Empty,
                    ["response"] = token,
                    ["remoteip"] = ip
                }), ct);

            if (!resposta.IsSuccessStatusCode)
                return VerificationVerdict.Indisponivel;

            var json = await resposta.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken: ct);
            return json is { Success: true } ? VerificationVerdict.Ok : VerificationVerdict.Invalido;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (ex is OperationCanceledException && ct.IsCancellationRequested)
                throw;
            return VerificationVerdict.Indisponivel;
        }
    }
}
