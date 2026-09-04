using Finort.Services;

namespace Finort.Tests;

public class TokenServiceTests
{
    [Fact]
    public void GenerateToken_IsNotEqualToItsHash()
    {
        var token = TokenService.GenerateToken();
        Assert.NotEqual(token, TokenService.HashToken(token));
        Assert.NotEmpty(token);
    }

    [Fact]
    public void HashToken_IsDeterministic()
    {
        Assert.Equal(TokenService.HashToken("abc"), TokenService.HashToken("abc"));
    }

    [Fact]
    public async Task Create_ReturnsRawToken_StoresHash()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            var configuracao = await auth.CriarConfiguracaoAsync("T", "t@t.com", "senha1234");
            var tokens = new TokenService(db);

            var raw = await tokens.CreateAsync(configuracao);
            var stored = await db.PasswordResetTokens.FindAsync(1);

            Assert.NotEmpty(raw);
            Assert.Equal(TokenService.HashToken(raw), stored!.TokenHash);
            Assert.True(stored.ExpiresAt > DateTime.UtcNow);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task Validate_ValidToken_ReturnsConfiguracao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            var configuracao = await auth.CriarConfiguracaoAsync("T", "t@t.com", "senha1234");
            var tokens = new TokenService(db);

            var raw = await tokens.CreateAsync(configuracao);
            var result = await tokens.ValidateAsync(raw);

            Assert.NotNull(result);
            Assert.Equal(configuracao.Id, result!.Id);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task Validate_UsedToken_ReturnsNull()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            var configuracao = await auth.CriarConfiguracaoAsync("T", "t@t.com", "senha1234");
            var tokens = new TokenService(db);

            var raw = await tokens.CreateAsync(configuracao);
            await tokens.MarkUsedAsync(raw);

            Assert.Null(await tokens.ValidateAsync(raw));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task Validate_ExpiredToken_ReturnsNull()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            var configuracao = await auth.CriarConfiguracaoAsync("T", "t@t.com", "senha1234");
            var tokens = new TokenService(db);

            var raw = await tokens.CreateAsync(configuracao, TimeSpan.FromMilliseconds(-1));

            Assert.Null(await tokens.ValidateAsync(raw));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task Validate_UnknownToken_ReturnsNull()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var tokens = new TokenService(db);
            Assert.Null(await tokens.ValidateAsync("nao-existe"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}