using System.Net.Mail;
using CI.Platform.Notifications.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace CI.Platform.Notifications.Infrastructure;

public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host      = config["Smtp:Host"] ?? "localhost";
        var port      = int.Parse(config["Smtp:Port"] ?? "1025");
        var from      = config["Smtp:From"] ?? "noreply@codinginnova.com";
        var enableSsl = bool.Parse(config["Smtp:EnableSsl"] ?? "false");
        var user      = config["Smtp:Username"];
        var password  = config["Smtp:Password"];

        using var smtp = new SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrEmpty(user))
            smtp.Credentials = new System.Net.NetworkCredential(user, password);

        var message = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };
        await smtp.SendMailAsync(message, ct);
        logger.LogDebug("Email sent to {To} subject='{Subject}'", to, subject);
    }
}
