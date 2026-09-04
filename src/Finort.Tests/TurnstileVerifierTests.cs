using System.Net;
using System.Text;
using Finort.Services;

namespace Finort.Tests;

public class TurnstileVerifierTests
{
    private sealed record Resposta(int Status, string Body);

    private sealed class Handler(params Resposta[] respostas) : HttpMessageHandler
    {
        private int _i;
        public FormUrlEncodedContent? UltimoCorpo { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Content is FormUrlEncodedContent f)
                UltimoCorpo = f;
            var r = respostas[Math.Min(_i++, respostas.Length - 1)];
            if (r.Status == 0)
                throw new HttpRequestException("sem rede");
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)r.Status)
            {
                Content = new StringContent(r.Body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static TurnstileVerifier Criar(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new TurnstileConfig { SiteKey = "sk", SecretKey = "sec" });

    [Fact]
    public async Task SiteverifyOk_RetornaOk_EEnviaSegreshave()
    {
        var handler = new Handler(new Resposta(200, """{"success":true}"""));
        var verdict = await Criar(handler).VerificarAsync("tok", "1.2.3.4");

        Assert.Equal(VerificationVerdict.Ok, verdict);
        var corpo = await handler.UltimoCorpo!.ReadAsStringAsync();
        Assert.Contains("secret=sec", corpo);
        Assert.Contains("response=tok", corpo);
        Assert.Contains("remoteip=1.2.3.4", corpo);
    }

    [Fact]
    public async Task SiteverifyFalha_RetornaInvalido()
    {
        var verdict = await Criar(new Handler(new Resposta(200, """{"success":false,"error-codes":["invalid-input-response"]}""")))
            .VerificarAsync("tok", "1.2.3.4");
        Assert.Equal(VerificationVerdict.Invalido, verdict);
    }

    [Fact]
    public async Task HttpNao200_RetornaIndisponivel()
    {
        var verdict = await Criar(new Handler(new Resposta(500, "{}"))).VerificarAsync("tok", "ip");
        Assert.Equal(VerificationVerdict.Indisponivel, verdict);
    }

    [Fact]
    public async Task SemConexao_RetornaIndisponivel()
    {
        var verdict = await Criar(new Handler(new Resposta(0, ""))).VerificarAsync("tok", "ip");
        Assert.Equal(VerificationVerdict.Indisponivel, verdict);
    }

    [Fact]
    public async Task CorpoMalformado200_RetornaIndisponivel()
    {
        var verdict = await Criar(new Handler(new Resposta(200, "isso nao e json")))
            .VerificarAsync("tok", "ip");
        Assert.Equal(VerificationVerdict.Indisponivel, verdict);
    }

    [Fact]
    public async Task CancelamentoPeloChamador_RepropagaOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Criar(new Handler(new Resposta(200, """{"success":true}""")))
                .VerificarAsync("tok", "ip", cts.Token));
    }
}
