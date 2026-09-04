namespace Finort.Models.Auth;

public record SmtpSettings(string? Host, int? Port, string? User, string? Password, string? From);