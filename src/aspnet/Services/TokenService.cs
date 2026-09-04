using System.Security.Cryptography;
using System.Text;
using Finort.Data;
using Finort.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class TokenService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(30);
    private readonly AppDbContext _db;

    public TokenService(AppDbContext db)
    {
        _db = db;
    }

    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<string> CreateAsync(Configuracao configuracao, TimeSpan? lifetime = null)
    {
        var token = GenerateToken();
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            TokenHash = HashToken(token),
            ConfiguracaoId = configuracao.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(lifetime ?? DefaultLifetime)
        });
        await _db.SaveChangesAsync();
        return token;
    }

    public async Task<Configuracao?> ValidateAsync(string rawToken)
    {
        var hash = HashToken(rawToken);
        var entity = await _db.PasswordResetTokens
            .Include(t => t.Configuracao)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (entity is null) return null;
        if (entity.UsedAt is not null) return null;
        if (entity.ExpiresAt < DateTime.UtcNow) return null;

        return entity.Configuracao;
    }

    public async Task MarkUsedAsync(string rawToken)
    {
        var hash = HashToken(rawToken);
        var entity = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (entity is not null)
        {
            entity.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}