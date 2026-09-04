using Finort.Data;
using Finort.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class AuthService
{
    /// <summary>Marcador do formato de armazenamento irreversível da senha de backup.</summary>
    public const string PrefixoHashBackup = "hash:";

    private readonly AppDbContext _db;
    private readonly SecretProtector? _secrets;
    private readonly PasswordHasher<Configuracao> _passwordHasher = new();

    public AuthService(AppDbContext db, SecretProtector? secrets = null)
    {
        _db = db;
        _secrets = secrets;
    }

    public async Task<Configuracao?> GetConfiguracaoAsync()
        => await _db.Configuracoes.FirstOrDefaultAsync();

    public bool VerificarSenha(Configuracao configuracao, string senha)
        => _passwordHasher.VerifyHashedPassword(configuracao, configuracao.SenhaHash, senha)
           != PasswordVerificationResult.Failed;

    public async Task<Configuracao> CriarConfiguracaoAsync(string nome, string email, string senha)
    {
        if (await _db.Configuracoes.AnyAsync())
            throw new InvalidOperationException("O acesso já foi configurado neste servidor.");

        var configuracao = new Configuracao
        {
            Nome = nome,
            Email = email,
            SenhaHash = _passwordHasher.HashPassword(null!, senha),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Configuracoes.Add(configuracao);
        await _db.SaveChangesAsync();
        return configuracao;
    }

    public async Task AlterarSenhaComVerificacaoAsync(Configuracao configuracao, string senhaAtual, string novaSenha)
    {
        if (_passwordHasher.VerifyHashedPassword(configuracao, configuracao.SenhaHash, senhaAtual)
            == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Senha atual incorreta.");

        configuracao.SenhaHash = _passwordHasher.HashPassword(configuracao, novaSenha);
        configuracao.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DefinirSenhaBackupAsync(Configuracao configuracao, string senhaBackup)
    {
        // Armazenamento irreversível: a senha de backup não pode ser recuperada,
        // apenas verificada (contra hash PBKDF2) ou substituída.
        configuracao.BackupPasswordCriptografada =
            PrefixoHashBackup + _passwordHasher.HashPassword(configuracao, senhaBackup);
        configuracao.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public static bool EstaEmFormatoHashBackup(string? armazenada)
        => armazenada?.StartsWith(PrefixoHashBackup, StringComparison.Ordinal) == true;

    public bool VerificarSenhaBackup(Configuracao configuracao, string senhaBackup)
    {
        var armazenada = configuracao.BackupPasswordCriptografada;
        if (string.IsNullOrEmpty(armazenada)) return false;

        if (EstaEmFormatoHashBackup(armazenada))
            return _passwordHasher.VerifyHashedPassword(
                       configuracao, armazenada[PrefixoHashBackup.Length..], senhaBackup)
                   != PasswordVerificationResult.Failed;

        // Formato legado reversível (existente apenas até a migração de startup concluir).
        try
        {
            return string.Equals(BackupCrypto.DecryptStringLegado(armazenada), senhaBackup,
                StringComparison.Ordinal);
        }
        catch { return false; }
    }

    public async Task AlterarSenhaAsync(Configuracao configuracao, string novaSenha)
    {
        configuracao.SenhaHash = _passwordHasher.HashPassword(configuracao, novaSenha);
        configuracao.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task AtualizarPerfilAsync(Configuracao configuracao, string nome, string email)
    {
        configuracao.Nome = nome;
        configuracao.Email = email;
        configuracao.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task AtualizarSmtpAsync(Configuracao configuracao, SmtpSettings smtp)
    {
        configuracao.SmtpHost = smtp.Host;
        configuracao.SmtpPort = smtp.Port;
        configuracao.SmtpUser = smtp.User;
        configuracao.SmtpPassword = smtp.Password is not null && _secrets is not null
            ? _secrets.Protect(smtp.Password)
            : smtp.Password;
        configuracao.SmtpFrom = smtp.From;
        configuracao.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}