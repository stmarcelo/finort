using Finort.Services;

namespace Finort.Tests;

public class LoginAttemptGuardTests
{
    private const int MaxAttempts = 5;

    private static string NovaOrigem() => "origem-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void IsLocked_Initially_NotLocked()
    {
        var origem = NovaOrigem();
        Assert.False(LoginAttemptGuard.IsLocked(origem));
    }

    [Fact]
    public void RecordFailure_BelowMax_NotLocked()
    {
        var origem = NovaOrigem();
        for (var i = 0; i < MaxAttempts - 1; i++)
        {
            LoginAttemptGuard.RecordFailure(origem);
        }
        Assert.False(LoginAttemptGuard.IsLocked(origem));
    }

    [Fact]
    public void RecordFailure_MaxFailures_Locks()
    {
        var origem = NovaOrigem();
        for (var i = 0; i < MaxAttempts; i++)
        {
            LoginAttemptGuard.RecordFailure(origem);
        }
        Assert.True(LoginAttemptGuard.IsLocked(origem));
    }

    [Fact]
    public void RecordFailure_WhileLocked_DoesNotThrow_StaysLocked()
    {
        var origem = NovaOrigem();
        for (var i = 0; i < MaxAttempts; i++)
        {
            LoginAttemptGuard.RecordFailure(origem);
        }
        Assert.True(LoginAttemptGuard.IsLocked(origem));

        LoginAttemptGuard.RecordFailure(origem);
        Assert.True(LoginAttemptGuard.IsLocked(origem));
    }

    [Fact]
    public void Reset_ClearsCounter_NotLocked()
    {
        var origem = NovaOrigem();
        for (var i = 0; i < MaxAttempts; i++)
        {
            LoginAttemptGuard.RecordFailure(origem);
        }
        Assert.True(LoginAttemptGuard.IsLocked(origem));

        LoginAttemptGuard.Reset(origem);
        Assert.False(LoginAttemptGuard.IsLocked(origem));
    }

    [Fact]
    public void Lockout_DeOutraOrigem_NaoAfeta()
    {
        var bloqueada = NovaOrigem();
        var livre = NovaOrigem();
        for (var i = 0; i < MaxAttempts; i++)
        {
            LoginAttemptGuard.RecordFailure(bloqueada);
        }
        Assert.True(LoginAttemptGuard.IsLocked(bloqueada));
        Assert.False(LoginAttemptGuard.IsLocked(livre));
    }

    [Fact]
    public void TentativasOrigem_SemFalhas_Zero()
    {
        Assert.Equal(0, LoginAttemptGuard.TentativasOrigem(NovaOrigem()));
    }

    [Fact]
    public void TentativasOrigem_AposFalhas_RetornaContagem()
    {
        var origem = NovaOrigem();
        LoginAttemptGuard.RecordFailure(origem);
        LoginAttemptGuard.RecordFailure(origem);
        Assert.Equal(2, LoginAttemptGuard.TentativasOrigem(origem));
    }

    [Fact]
    public void TentativasOrigem_AposLockout_Zera()
    {
        var origem = NovaOrigem();
        for (var i = 0; i < MaxAttempts; i++)
            LoginAttemptGuard.RecordFailure(origem);
        Assert.True(LoginAttemptGuard.IsLocked(origem));
        Assert.Equal(0, LoginAttemptGuard.TentativasOrigem(origem));
    }

    [Fact]
    public void TentativasOrigem_AposReset_Zero()
    {
        var origem = NovaOrigem();
        LoginAttemptGuard.RecordFailure(origem);
        LoginAttemptGuard.Reset(origem);
        Assert.Equal(0, LoginAttemptGuard.TentativasOrigem(origem));
    }
}
