using Finort.Models.Auth;

namespace Finort.Services;

public enum LoginStatus
{
    Ok,
    SenhaIncorreta,
    Bloqueado,
    DesafioInvalido,
    DesafioIndisponivel
}

public sealed record LoginResult(
    LoginStatus Status,
    Configuracao? Configuracao,
    bool RequireTurnstile = false);

public sealed record LoginRequest(string Senha, string? TurnstileToken = null);

public class AuthLoginService
{
    public const int LimiarDesafio = 2;

    private readonly AuthService _authService;
    private readonly IPageVerifier _pageVerifier;
    private readonly TurnstileConfig _turnstile;

    public AuthLoginService(AuthService authService, IPageVerifier pageVerifier, TurnstileConfig turnstile)
    {
        _authService = authService;
        _pageVerifier = pageVerifier;
        _turnstile = turnstile;
    }

    private bool DesafioAtivo(string origem) =>
        _turnstile.Configurado && LoginAttemptGuard.TentativasOrigem(origem) >= LimiarDesafio;

    public async Task<LoginResult> ValidarAsync(string senha, string origem, string? turnstileToken = null)
    {
        if (LoginAttemptGuard.IsLocked(origem))
            return new LoginResult(LoginStatus.Bloqueado, null, DesafioAtivo(origem));

        // O desafio só existe quando já há configuração de login: antes do primeiro
        // setup não há senha a proteger e o auto-login do PrimeiroAcesso não pode ser bloqueado.
        var configuracao = await _authService.GetConfiguracaoAsync();

        if (configuracao is not null && DesafioAtivo(origem))
        {
            if (string.IsNullOrWhiteSpace(turnstileToken))
            {
                LoginAttemptGuard.RecordFailure(origem);
                return new LoginResult(LoginStatus.DesafioInvalido, null, true);
            }

            var veredito = await _pageVerifier.VerificarAsync(turnstileToken, origem);
            if (veredito == VerificationVerdict.Indisponivel)
                return new LoginResult(LoginStatus.DesafioIndisponivel, null, true);
            if (veredito == VerificationVerdict.Invalido)
            {
                LoginAttemptGuard.RecordFailure(origem);
                return new LoginResult(LoginStatus.DesafioInvalido, null, true);
            }
        }

        if (configuracao is null)
        {
            LoginAttemptGuard.RecordFailure(origem);
            return new LoginResult(LoginStatus.SenhaIncorreta, null, false);
        }

        if (!_authService.VerificarSenha(configuracao, senha))
        {
            LoginAttemptGuard.RecordFailure(origem);
            return new LoginResult(LoginStatus.SenhaIncorreta, null, DesafioAtivo(origem));
        }

        LoginAttemptGuard.Reset(origem);
        return new LoginResult(LoginStatus.Ok, configuracao);
    }
}
