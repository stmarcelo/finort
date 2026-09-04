using System.Security.Claims;
using Finort.App;

namespace Finort.Tests;

public class ScopedAuthenticationStateProviderTests
{
    private static ScopedAuthenticationStateProvider Create()
        => new(new LoginState());

    [Fact]
    public async Task GetAuthenticationState_SemSignIn_ReturnsAnonymous()
    {
        var provider = Create();

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task SignIn_ThenGetState_Authenticated()
    {
        var provider = Create();
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "Maria") }, "app");
        provider.SignIn(new ClaimsPrincipal(identity));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("Maria", state.User.Identity?.Name);
    }

    [Fact]
    public async Task SignOut_ClearsPrincipal()
    {
        var provider = Create();
        provider.SignIn(new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Maria") }, "app")));
        provider.SignOut();

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task EstadoNaoCompartilhadoEntreInstancias_NovaAberturaExigeLogin()
    {
        var circuitoAntigo = Create();
        circuitoAntigo.SignIn(new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Maria") }, "app")));

        var novaAbertura = Create();
        var state = await novaAbertura.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }
}
