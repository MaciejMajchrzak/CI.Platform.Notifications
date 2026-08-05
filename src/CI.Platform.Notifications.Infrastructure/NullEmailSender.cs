using CI.Platform.Notifications.Core;
using Microsoft.Extensions.Logging;
namespace CI.Platform.Notifications.Infrastructure;

public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("[NullEmailSender] Would send to {To} subject='{Subject}'", to, subject);
        return Task.CompletedTask;
    }
}
