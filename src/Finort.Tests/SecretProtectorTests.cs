using Finort.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Finort.Tests;

public class SecretProtectorTests
{
    private static SecretProtector Criar()
    {
        var provider = new ServiceCollection()
            .AddDataProtection()
            .Services
            .BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();
        return new SecretProtector(provider);
    }

    [Fact]
    public void Protect_Unprotect_RoundTrip()
    {
        var protector = Criar();

        var protegida = protector.Protect("senha-smtp-123");

        Assert.StartsWith(SecretProtector.Prefixo, protegida);
        Assert.DoesNotContain("senha-smtp-123", protegida);
        Assert.Equal("senha-smtp-123", protector.Unprotect(protegida));
    }

    [Fact]
    public void Unprotect_ValorLegadoSemPrefixo_RetornaOriginal()
    {
        var protector = Criar();

        Assert.Equal("texto-plano", protector.Unprotect("texto-plano"));
        Assert.Null(protector.Unprotect(null));
        Assert.False(SecretProtector.PareceProtegido("texto-plano"));
        Assert.True(SecretProtector.PareceProtegido("dp:qualquer"));
    }
}
