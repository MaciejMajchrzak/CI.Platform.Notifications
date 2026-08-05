namespace CI.Platform.Notifications.Core;

public interface INotificationSender
{
    Task SendAsync(
        string                      code,
        Guid                        tenantId,
        string?                     userId,
        string                      recipientEmail,
        string                      language,
        Dictionary<string, object?> data,
        string?                     idempotencyKey = null,
        CancellationToken           ct             = default);
}
