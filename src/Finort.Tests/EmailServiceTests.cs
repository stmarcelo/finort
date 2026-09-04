using Finort.Models.Auth;
using Finort.Services;
using MailKit.Security;
using MimeKit;

namespace Finort.Tests;

public class EmailServiceTests
{
    [Fact]
    public void IsConfigured_EmptySmtp_ReturnsFalse()
    {
        var c = new Configuracao { Nome = "T", Email = "t@t.com" };
        Assert.False(EmailService.IsConfigured(c));
    }

    [Fact]
    public void IsConfigured_HostAndPort_ReturnsTrue()
    {
        var c = new Configuracao { Nome = "T", Email = "t@t.com", SmtpHost = "smtp.x.com", SmtpPort = 587 };
        Assert.True(EmailService.IsConfigured(c));
    }

    [Theory]
    [InlineData(465, SecureSocketOptions.SslOnConnect)]
    [InlineData(587, SecureSocketOptions.StartTlsWhenAvailable)]
    [InlineData(25, SecureSocketOptions.Auto)]
    public void GetSecureSocketOptions_Port_Maps(int port, SecureSocketOptions expected)
    {
        Assert.Equal(expected, EmailService.GetSecureSocketOptions(port));
    }

    [Fact]
    public void BuildMessage_SetsFields()
    {
        var message = EmailService.BuildMessage("a@x.com", "b@x.com", "Assunto", "Corpo");
        Assert.Equal("a@x.com", message.From.Mailboxes.First().Address);
        Assert.Equal("b@x.com", message.To.Mailboxes.First().Address);
        Assert.Equal("Assunto", message.Subject);
        Assert.Equal("Corpo", Assert.IsType<TextPart>(message.Body).Text.Trim());
    }
}