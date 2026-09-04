using System.Security.Claims;

namespace Finort.App;

public class LoginState
{
    public ClaimsPrincipal? Principal { get; private set; }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public void SetAuthenticated(ClaimsPrincipal principal) => Principal = principal;

    public void Clear() => Principal = null;
}