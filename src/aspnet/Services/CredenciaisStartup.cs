using Finort.Data;
using Finort.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

/// <summary>Rotina de startup: publica a chave de primeiro acesso e converte credenciais
/// legadas (senha de backup reversível → hash; senha SMTP em texto plano → Data Protection).</summary>
public class CredenciaisStartup
{
    private readonly AppDbContext _db;
    private readonly SecretProtector _secrets;
    private readonly SetupKeyStore _setupKeys;
    private readonly ILogger<CredenciaisStartup> _logger;
    private static readonly PasswordHasher<Configuracao> Hasher = new();

    public CredenciaisStartup(AppDbContext db, SecretProtector secrets,
        SetupKeyStore setupKeys, ILogger<CredenciaisStartup> logger)
    {
        _db = db;
        _secrets = secrets;
        _setupKeys = setupKeys;
        _logger = logger;
    }

    public async Task ExecutarAsync()
    {
        var config = await _db.Configuracoes.FirstOrDefaultAsync();

        if (config is null)
        {
            var chave = _setupKeys.GerarSeNecessario();
            _logger.LogWarning(
                "Primeiro acesso: use a chave de configuração {Chave} na tela /configurar.", chave);
            return;
        }

        _setupKeys.Remover();
        var alterado = false;

        var armazenada = config.BackupPasswordCriptografada;
        if (!string.IsNullOrEmpty(armazenada) && !AuthService.EstaEmFormatoHashBackup(armazenada))
        {
            try
            {
                var senhaLegada = BackupCrypto.DecryptStringLegado(armazenada);
                config.BackupPasswordCriptografada = Hasher.HashPassword(config, senhaLegada);
                alterado = true;
                _logger.LogInformation("Senha de backup migrada para hash irreversível.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Não foi possível migrar a senha de backup legada; ela será substituída " +
                    "quando o usuário definir uma nova senha de backup.");
            }
        }

        if (!string.IsNullOrEmpty(config.SmtpPassword) &&
            !SecretProtector.PareceProtegido(config.SmtpPassword))
        {
            config.SmtpPassword = _secrets.Protect(config.SmtpPassword);
            alterado = true;
            _logger.LogInformation("Senha SMTP protegida com as chaves locais de Data Protection.");
        }

        if (alterado)
            await _db.SaveChangesAsync();
    }
}
