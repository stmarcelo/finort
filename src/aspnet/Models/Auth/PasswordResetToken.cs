using System.ComponentModel.DataAnnotations;

namespace Finort.Models.Auth;

public class PasswordResetToken
{
    public int Id { get; set; }

    [Required]
    public string TokenHash { get; set; } = string.Empty;

    public int ConfiguracaoId { get; set; }
    public Configuracao Configuracao { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}