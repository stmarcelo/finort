using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Auth;

public class Configuracao
{
    public int Id { get; set; }

    [Required]
    public string Nome { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    public string SenhaHash { get; set; } = string.Empty;

    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFrom { get; set; }

    public string? BackupPasswordCriptografada { get; set; }

    public DateTime? UltimaVerificacaoVersao { get; set; }
    public string? VersaoConhecida { get; set; }

    /// <summary>Dias do mês seguinte a incluir no fluxo do mês atual (0-15).</summary>
    public int DiasAntecipacao { get; set; } = 5;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}