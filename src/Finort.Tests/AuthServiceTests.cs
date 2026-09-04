using Finort.Data;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task CriarConfiguracao_HashesPassword_And_Verifies()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            var configuracao = await service.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");

            Assert.NotEqual("senha1234", configuracao.SenhaHash);
            Assert.True(service.VerificarSenha(configuracao, "senha1234"));
            Assert.False(service.VerificarSenha(configuracao, "errada"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task AlterarSenha_OldFails_NewWorks()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            var configuracao = await service.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");

            await service.AlterarSenhaAsync(configuracao, "novaSenha99");

            Assert.False(service.VerificarSenha(configuracao, "senha1234"));
            Assert.True(service.VerificarSenha(configuracao, "novaSenha99"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task AlterarSenhaComVerificacao_SenhaAtualErrada_Lanca()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            var configuracao = await service.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AlterarSenhaComVerificacaoAsync(configuracao, "errada", "nova456"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task AlterarSenhaComVerificacao_TrocaESenhaNovaValida()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            var configuracao = await service.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");

            await service.AlterarSenhaComVerificacaoAsync(configuracao, "senha1234", "nova456");

            Assert.False(service.VerificarSenha(configuracao, "senha1234"));
            Assert.True(service.VerificarSenha(configuracao, "nova456"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task SenhaBackup_DefinirEVerificar()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            var configuracao = await service.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");

            await service.DefinirSenhaBackupAsync(configuracao, "bkp999");

            Assert.True(service.VerificarSenhaBackup(configuracao, "bkp999"));
            Assert.False(service.VerificarSenhaBackup(configuracao, "errada"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public void SenhaBackup_SemHashDefinida_RetornaFalse()
    {
        using var db = new AppDbContext(new DbContextOptions<AppDbContext>());
        var service = new AuthService(db);
        var configuracao = new Finort.Models.Auth.Configuracao();

        Assert.False(service.VerificarSenhaBackup(configuracao, "qualquer"));
    }

    [Fact]
    public async Task GetConfiguracao_WithoutRow_ReturnsNull()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            Assert.Null(await service.GetConfiguracaoAsync());
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task CriarConfiguracao_QuandoJaExiste_Lanca()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            await service.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CriarConfiguracaoAsync("Invasor", "x@x.com", "senha9876"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public async Task SenhaBackup_ArmazenadaComoHash_NaoReversivel()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var service = new AuthService(db);
            var configuracao = await service.CriarConfiguracaoAsync("Teste", "teste@teste.com", "senha1234");

            await service.DefinirSenhaBackupAsync(configuracao, "bkp999");

            Assert.StartsWith(AuthService.PrefixoHashBackup, configuracao.BackupPasswordCriptografada!);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}