using Finort.Data;
using Finort.Models.Auth;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Finort.Services;

public class EmailService
{
    private readonly AppDbContext _db;
    private readonly SecretProtector? _secrets;

    public EmailService(AppDbContext db, SecretProtector? secrets = null)
    {
        _db = db;
        _secrets = secrets;
    }

    public static bool IsConfigured(Configuracao configuracao)
        => !string.IsNullOrWhiteSpace(configuracao.SmtpHost) && configuracao.SmtpPort is not null;

    public static SecureSocketOptions GetSecureSocketOptions(int port)
        => port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTlsWhenAvailable,
            25 => SecureSocketOptions.Auto,
            _ => SecureSocketOptions.Auto
        };

    public static MimeMessage BuildMessage(string from, string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    public async Task<bool> IsConfiguredAsync()
    {
        var configuracao = await _db.Configuracoes.FirstOrDefaultAsync();
        return configuracao is not null && IsConfigured(configuracao);
    }

    public async Task SendAsync(string subject, string body)
    {
        var configuracao = await _db.Configuracoes.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Configuracao não existe.");
        if (!IsConfigured(configuracao))
            throw new InvalidOperationException("SMTP não configurado.");

        await SendCoreAsync(
            configuracao.SmtpHost!, configuracao.SmtpPort!.Value,
            configuracao.SmtpUser, _secrets?.Unprotect(configuracao.SmtpPassword) ?? configuracao.SmtpPassword,
            configuracao.SmtpFrom ?? configuracao.Email,
            configuracao.Email, subject, body);
    }

    public Task SendTestAsync(SmtpSettings settings, string to, string subject, string body)
        => SendCoreAsync(settings.Host!, settings.Port!.Value, settings.User,
            settings.Password, settings.From ?? to, to, subject, body);

    private static async Task SendCoreAsync(
        string host, int port, string? user, string? password,
        string from, string to, string subject, string body)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, GetSecureSocketOptions(port));
        if (!string.IsNullOrEmpty(user))
        {
            await client.AuthenticateAsync(user, password ?? string.Empty);
        }
        await client.SendAsync(BuildMessage(from, to, subject, body));
        await client.DisconnectAsync(true);
    }
}