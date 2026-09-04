using System.Collections.Concurrent;

namespace Finort.Services;

/// <summary>Bloqueio de tentativas de login por origem (IP). Evita lockout global
/// (negação de acesso ao dono) quando a aplicação está exposta em servidor.</summary>
public static class LoginAttemptGuard
{
    internal const int MaxFailedAttempts = 5;
    internal const int LockoutDurationSeconds = 60;
    private const int LimpezaAcimaDe = 1024;

    private sealed class Estado
    {
        public int Tentativas;
        public DateTime BloqueadoAteUtc;
    }

    private static readonly ConcurrentDictionary<string, Estado> Estados = new();

    public static bool IsLocked(string origem)
        => Estados.TryGetValue(origem, out var estado) && DateTime.UtcNow < estado.BloqueadoAteUtc;

    /// <summary>Falhas na janela corrente da origem (0 quando ausente ou após lockout).</summary>
    public static int TentativasOrigem(string origem)
    {
        if (!Estados.TryGetValue(origem, out var estado))
            return 0;
        lock (estado)
        {
            return DateTime.UtcNow < estado.BloqueadoAteUtc ? 0 : estado.Tentativas;
        }
    }

    public static void RecordFailure(string origem)
    {
        var estado = Estados.GetOrAdd(origem, static _ => new Estado());
        lock (estado)
        {
            if (DateTime.UtcNow < estado.BloqueadoAteUtc)
                return;

            estado.Tentativas++;
            if (estado.Tentativas < MaxFailedAttempts)
                return;

            estado.BloqueadoAteUtc = DateTime.UtcNow.AddSeconds(LockoutDurationSeconds);
            estado.Tentativas = 0;
        }

        if (Estados.Count > LimpezaAcimaDe)
            Purgar();
    }

    public static void Reset(string origem) => Estados.TryRemove(origem, out _);

    private static void Purgar()
    {
        var agora = DateTime.UtcNow;
        foreach (var par in Estados)
        {
            lock (par.Value)
            {
                if (par.Value.Tentativas == 0 && par.Value.BloqueadoAteUtc <= agora)
                    Estados.TryRemove(par.Key, out _);
            }
        }
    }
}
