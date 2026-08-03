namespace CI.Platform.Notifications.Core.DTOs;

public sealed record NotificationLogDto(
    Guid Id,
    Guid TenantId,
    string Channel,
    string Recipient,
    string TemplateKey,
    string Status,
    DateTimeOffset? SentAt,
    string? FailureReason,
    string? IdempotencyKey,
    DateTimeOffset CreatedAt,
    uint RowVersion);
