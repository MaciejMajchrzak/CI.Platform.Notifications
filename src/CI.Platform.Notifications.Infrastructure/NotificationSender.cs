using CI.Platform.Notifications.Core;
using CI.Platform.Notifications.Domain.Entities;
using Microsoft.Extensions.Logging;
namespace CI.Platform.Notifications.Infrastructure;

public sealed class NotificationSender(
    INotificationsRepository     repo,
    IEmailSender                 emailSender,
    ILogger<NotificationSender>  logger) : INotificationSender
{
    public async Task SendAsync(
        string code, Guid tenantId, string? userId, string recipientEmail,
        string language, Dictionary<string, object?> data,
        string? idempotencyKey = null, CancellationToken ct = default)
    {
        var definition = await repo.FindDefinitionAsync(code, ct);
        if (definition is null)
        {
            logger.LogWarning("Notification definition '{Code}' not found — skipping", code);
            return;
        }

        var channels = (NotificationChannelFlags)definition.DefaultChannels;

        if (channels.HasFlag(NotificationChannelFlags.Email))
            await SendEmailAsync(definition, language, recipientEmail, data, tenantId, idempotencyKey, ct);

        if (channels.HasFlag(NotificationChannelFlags.InApp) && userId is not null)
            await SendInAppAsync(definition, language, tenantId, userId, data, ct);
    }

    private async Task SendEmailAsync(
        NotificationDefinition definition, string language, string recipient,
        Dictionary<string, object?> data, Guid tenantId, string? idempotencyKey, CancellationToken ct)
    {
        var template = await repo.FindTemplateAsync(definition.Code, NotificationChannel.Email, language, ct)
                    ?? await repo.FindTemplateAsync(definition.Code, NotificationChannel.Email, "en", ct);

        if (template is null)
        {
            logger.LogWarning("No email template for {Code}/{Lang} — skipping email", definition.Code, language);
            return;
        }

        var subject = Render(template.SubjectTemplate ?? definition.Name, data);
        var body    = Render(template.BodyTemplate, data);

        var status        = NotificationStatus.Sent;
        string? failureReason = null;
        try
        {
            await emailSender.SendAsync(recipient, subject, body, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email notification {Code} to {Recipient}", definition.Code, recipient);
            status        = NotificationStatus.Failed;
            failureReason = ex.Message;
        }

        var log = new NotificationLog
        {
            TenantId       = tenantId,
            Channel        = NotificationChannel.Email,
            Recipient      = recipient,
            TemplateKey    = definition.Code,
            Status         = status,
            SentAt         = status == NotificationStatus.Sent ? DateTimeOffset.UtcNow : null,
            FailureReason  = failureReason,
            IdempotencyKey = idempotencyKey,
        };
        await repo.AddLogAsync(log, ct);
        await repo.SaveChangesAsync(ct);
    }

    private async Task SendInAppAsync(
        NotificationDefinition definition, string language, Guid tenantId, string userId,
        Dictionary<string, object?> data, CancellationToken ct)
    {
        var template = await repo.FindTemplateAsync(definition.Code, NotificationChannel.InApp, language, ct)
                    ?? await repo.FindTemplateAsync(definition.Code, NotificationChannel.InApp, "en", ct);

        if (template is null) return;

        var title = Render(template.SubjectTemplate ?? definition.Name, data);
        var body  = Render(template.BodyTemplate, data);

        await repo.AddInboxItemAsync(new NotificationInbox
        {
            TenantId = tenantId,
            UserId   = userId,
            Code     = definition.Code,
            Title    = title,
            Body     = body,
        }, ct);
        await repo.SaveChangesAsync(ct);
    }

    private static string Render(string template, Dictionary<string, object?> data)
        => data.Aggregate(template, (t, kv) =>
            t.Replace("{{" + kv.Key + "}}", kv.Value?.ToString() ?? string.Empty));
}
