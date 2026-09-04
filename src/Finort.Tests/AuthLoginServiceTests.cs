using Finort.Services;

namespace Finort.Tests;

public class AuthLoginServiceTests
{
    private readonly string Origem = "test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task ValidarAsync_SenhaCorreta_ReturnsOk()
    {
        LoginAttemptGuard.Reset(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var authService = new AuthService(db);
            var configuracao = await authService.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");
            var service = new AuthLoginService(authService, new VerifierFake(), new TurnstileConfig());

            var result = await service.ValidarAsync("senha1234", Origem);

            Assert.Equal(LoginStatus.Ok, result.Status);
            Assert.NotNull(result.Configuracao);
            Assert.Equal("Teste", result.Configuracao!.Nome);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ValidarAsync_SenhaIncorreta_ReturnsSenhaIncorreta()
    {
        LoginAttemptGuard.Reset(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var authService = new AuthService(db);
            await authService.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");
            var service = new AuthLoginService(authService, new VerifierFake(), new TurnstileConfig());

            var result = await service.ValidarAsync("errada", Origem);

            Assert.Equal(LoginStatus.SenhaIncorreta, result.Status);
            Assert.Null(result.Configuracao);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ValidarAsync_SemConfiguracao_ReturnsSenhaIncorreta()
    {
        LoginAttemptGuard.Reset(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthLoginService(new AuthService(db), new VerifierFake(), new TurnstileConfig());

            var result = await service.ValidarAsync("qualquer", Origem);

            Assert.Equal(LoginStatus.SenhaIncorreta, result.Status);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ValidarAsync_Bloqueado_ReturnsBloqueado()
    {
        LoginAttemptGuard.Reset(Origem);
        for (var i = 0; i < 5; i++)
        {
            LoginAttemptGuard.RecordFailure(Origem);
        }
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthLoginService(new AuthService(db), new VerifierFake(), new TurnstileConfig());

            var result = await service.ValidarAsync("qualquer", Origem);

            Assert.Equal(LoginStatus.Bloqueado, result.Status);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ValidarAsync_Bloqueado_EmOutraOrigem_NaoBloqueia()
    {
        var atacante = "att-" + Guid.NewGuid().ToString("N");
        var dono = "own-" + Guid.NewGuid().ToString("N");
        LoginAttemptGuard.Reset(atacante);
        LoginAttemptGuard.Reset(dono);
        var (db, file) = TestDbContext.Create();
        try
        {
            var authService = new AuthService(db);
            await authService.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");
            var service = new AuthLoginService(authService, new VerifierFake(), new TurnstileConfig());

            for (var i = 0; i < 5; i++)
            {
                await service.ValidarAsync("errada", atacante);
            }

            var bloqueado = await service.ValidarAsync("errada", atacante);
            var livre = await service.ValidarAsync("senha1234", dono);

            Assert.Equal(LoginStatus.Bloqueado, bloqueado.Status);
            Assert.Equal(LoginStatus.Ok, livre.Status);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
            LoginAttemptGuard.Reset(atacante);
            LoginAttemptGuard.Reset(dono);
        }
    }

    [Fact]
    public async Task ValidarAsync_Sucesso_ResetaGuard()
    {
        LoginAttemptGuard.Reset(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var authService = new AuthService(db);
            var configuracao = await authService.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");
            var service = new AuthLoginService(authService, new VerifierFake(), new TurnstileConfig());

            for (var i = 0; i < 4; i++)
            {
                await service.ValidarAsync("errada", Origem);
            }
            var ok = await service.ValidarAsync("senha1234", Origem);
            var after = await service.ValidarAsync("errada", Origem);

            Assert.Equal(LoginStatus.Ok, ok.Status);
            Assert.Equal(LoginStatus.SenhaIncorreta, after.Status);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task ValidarAsync_SemConfiguracao_ChallengeNuncaAtivo()
    {
        LoginAttemptGuard.Reset(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var verifier = new VerifierFake();
            var service = Servico(new AuthService(db), verifier, ComTurnstile());

            LoginAttemptGuard.RecordFailure(Origem);
            LoginAttemptGuard.RecordFailure(Origem);

            var r = await service.ValidarAsync("qualquer", Origem);

            Assert.Equal(LoginStatus.SenhaIncorreta, r.Status);
            Assert.False(r.RequireTurnstile);
            Assert.Equal(0, verifier.Chamadas);
        }
        finally { TestDbContext.Cleanup(db, file); LoginAttemptGuard.Reset(Origem); }
    }

    private sealed class VerifierFake(VerificationVerdict veredito = VerificationVerdict.Ok) : IPageVerifier
    {
        public int Chamadas { get; private set; }
        public Task<VerificationVerdict> VerificarAsync(string token, string ip, CancellationToken ct = default)
        {
            Chamadas++;
            return Task.FromResult(veredito);
        }
    }

    private static TurnstileConfig ComTurnstile() => new() { SiteKey = "site", SecretKey = "secret" };

    private static AuthLoginService Servico(AuthService auth, IPageVerifier verifier, TurnstileConfig cfg)
        => new(auth, verifier, cfg);

    [Fact]
    public async Task TurnstileDesligado_MuitasFalhas_NuncaExigeDesafio()
    {
        LoginAttemptGuard.Reset(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            await auth.CriarConfiguracaoAsync("Teste", "t@t.com", "senha1234");
            var verifier = new VerifierFake();
            var service = Servico(auth, verifier, new TurnstileConfig());

            for (var i = 0; i < 3; i++)
            {
                var r = await service.ValidarAsync("errada", Origem);
                Assert.Equal(LoginStatus.SenhaIncorreta, r.Status);
                Assert.False(r.RequireTurnstile);
            }
            Assert.Equal(0, verifier.Chamadas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task TurnstileLigado_DesafioApontaNaSegundaFalha()
    {
        LoginAttemptGuard.Reset(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            await auth.CriarConfiguracaoAsync("Teste", "t@t.com", "senha1234");
            var service = Servico(auth, new VerifierFake(), ComTurnstile());

            var primeira = await service.ValidarAsync("errada", Origem);
            var segunda = await service.ValidarAsync("errada", Origem);

            Assert.False(primeira.RequireTurnstile);
            Assert.True(segunda.RequireTurnstile);
        }
        finally { TestDbContext.Cleanup(db, file); LoginAttemptGuard.Reset(Origem); }
    }

    [Fact]
    public async Task DesafioAtivo_SemToken_DesafioInvalido_VerifierNaoChamada()
    {
        LoginAttemptGuard.Reset(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            await auth.CriarConfiguracaoAsync("Teste", "t@t.com", "senha1234");
            var verifier = new VerifierFake();
            var service = Servico(auth, verifier, ComTurnstile());

            var r = await service.ValidarAsync("senha1234", Origem);

            Assert.Equal(LoginStatus.DesafioInvalido, r.Status);
            Assert.True(r.RequireTurnstile);
            Assert.Equal(0, verifier.Chamadas);
        }
        finally { TestDbContext.Cleanup(db, file); LoginAttemptGuard.Reset(Origem); }
    }

    [Fact]
    public async Task DesafioAtivo_TokenInvalido_DesafioInvalido()
    {
        LoginAttemptGuard.Reset(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            await auth.CriarConfiguracaoAsync("Teste", "t@t.com", "senha1234");
            var service = Servico(auth, new VerifierFake(VerificationVerdict.Invalido), ComTurnstile());

            var r = await service.ValidarAsync("senha1234", Origem, "tok");

            Assert.Equal(LoginStatus.DesafioInvalido, r.Status);
        }
        finally { TestDbContext.Cleanup(db, file); LoginAttemptGuard.Reset(Origem); }
    }

    [Fact]
    public async Task DesafioAtivo_VerifierIndisponivel_FalhaSemContarComoTentativa()
    {
        LoginAttemptGuard.Reset(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            await auth.CriarConfiguracaoAsync("Teste", "t@t.com", "senha1234");
            var service = Servico(auth, new VerifierFake(VerificationVerdict.Indisponivel), ComTurnstile());

            var r = await service.ValidarAsync("senha1234", Origem, "tok");

            Assert.Equal(LoginStatus.DesafioIndisponivel, r.Status);
            Assert.Equal(2, LoginAttemptGuard.TentativasOrigem(Origem));
        }
        finally { TestDbContext.Cleanup(db, file); LoginAttemptGuard.Reset(Origem); }
    }

    [Fact]
    public async Task DesafioAtivo_TokenValido_SenhaErrada_ValidaSenhaAposSiteverify()
    {
        LoginAttemptGuard.Reset(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            await auth.CriarConfiguracaoAsync("Teste", "t@t.com", "senha1234");
            var verifier = new VerifierFake();
            var service = Servico(auth, verifier, ComTurnstile());

            var r = await service.ValidarAsync("errada", Origem, "tok");

            Assert.Equal(LoginStatus.SenhaIncorreta, r.Status);
            Assert.Equal(1, verifier.Chamadas);
        }
        finally { TestDbContext.Cleanup(db, file); LoginAttemptGuard.Reset(Origem); }
    }

    [Fact]
    public async Task DesafioAtivo_TokenValido_SenhaCorreta_OkEResetaContador()
    {
        LoginAttemptGuard.Reset(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        LoginAttemptGuard.RecordFailure(Origem);
        var (db, file) = TestDbContext.Create();
        try
        {
            var auth = new AuthService(db);
            await auth.CriarConfiguracaoAsync("Teste", "t@t.com", "senha1234");
            var service = Servico(auth, new VerifierFake(), ComTurnstile());

            var r = await service.ValidarAsync("senha1234", Origem, "tok");

            Assert.Equal(LoginStatus.Ok, r.Status);
            Assert.False(r.RequireTurnstile);
            Assert.Equal(0, LoginAttemptGuard.TentativasOrigem(Origem));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
