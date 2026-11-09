using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ILAQA.Services;

// Sends plain-text emails (verification and reset codes) through Gmail SMTP.
public sealed class EmailService(IConfiguration configuration)
{
    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var senderEmail = configuration["EmailSettings:SenderEmail"];
        var appPassword = configuration["EmailSettings:AppPassword"];
        var senderName = configuration["EmailSettings:SenderName"] ?? "ILAQA";

        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(appPassword))
            throw new InvalidOperationException("Gmail SMTP settings are missing.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(senderEmail, appPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
