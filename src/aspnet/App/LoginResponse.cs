namespace Finort.App;

public sealed record LoginResponse(
    int Status,
    string? Nome,
    bool RequireTurnstile,
    string? SiteKey);
