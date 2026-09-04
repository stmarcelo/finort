using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Finort.App;

public class ScopedAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly LoginState _loginState;

    public ScopedAuthenticationStateProvider(LoginState loginState)
    {
        _loginState = loginState;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = _loginState.Principal ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(principal));
    }

    public void SignIn(ClaimsPrincipal principal)
    {
        _loginState.SetAuthenticated(principal);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    public void SignOut()
    {
        _loginState.Clear();
        var anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        NotifyAuthenticationStateChanged(Task.FromResult(anonymous));
    }
}
