using Microsoft.AspNetCore.DataProtection;

namespace Finort.Services;

/// <summary>Protege segredos reutilizáveis (ex.: senha SMTP) com Data Protection do
/// ASP.NET Core. As chaves ficam no keyring local da instalação, nunca no código-fonte.</summary>
public class SecretProtector
{
    public const string Prefixo = "dp:";

    private readonly IDataProtector _protetor;

    public SecretProtector(IDataProtectionProvider provider)
        => _protetor = provider.CreateProtector("Finort.Secrets.v1");

    public string Protect(string valor)
        => Prefixo + _protetor.Protect(valor);

    public string? Unprotect(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return valor;
        return valor.StartsWith(Prefixo, StringComparison.Ordinal)
            ? _protetor.Unprotect(valor[Prefixo.Length..])
            : valor;
    }

    public static bool PareceProtegido(string? valor)
        => valor?.StartsWith(Prefixo, StringComparison.Ordinal) == true;
}
