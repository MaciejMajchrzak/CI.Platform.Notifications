using CI.Kernel;
namespace CI.Platform.Notifications.Domain.Entities;

public sealed class NotificationLog : BaseEntity
{
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string Status { get; set; } = NotificationStatus.Queued;
    public DateTimeOffset? SentAt { get; set; }
    public string? FailureReason { get; set; }
    public string? IdempotencyKey { get; set; }
}

public static class NotificationStatus
{
    public const string Queued = "Queued";
    public const string Sent   = "Sent";
    public const string Failed = "Failed";
}
