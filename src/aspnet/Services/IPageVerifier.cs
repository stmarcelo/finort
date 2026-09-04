namespace Finort.Services;

public enum VerificationVerdict
{
    Ok,
    Invalido,
    Indisponivel
}

public interface IPageVerifier
{
    Task<VerificationVerdict> VerificarAsync(string token, string ip, CancellationToken ct = default);
}
