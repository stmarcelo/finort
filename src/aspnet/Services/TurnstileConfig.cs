namespace Finort.Services;

/// <summary>Chaves do Cloudflare Turnstile vindas de appsettings/env.
/// Sem ambas, a feature fica desligada.</summary>
public sealed class TurnstileConfig
{
    public string? SiteKey { get; init; }
    public string? SecretKey { get; init; }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey);

    public static TurnstileConfig FromConfiguration(IConfiguration configuration) => new()
    {
        SiteKey = configuration["Turnstile:SiteKey"],
        SecretKey = configuration["Turnstile:SecretKey"]
    };
}
