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

public sealed record NotificationInboxDto(
    Guid Id,
    string Code,
    string Title,
    string Body,
    bool IsRead,
    string? DeepLinkUrl,
    DateTimeOffset CreatedAt);

public sealed record NotificationDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    int DefaultChannels,
    bool IsSystem);

public sealed record NotificationTemplateDto(
    Guid Id,
    Guid DefinitionId,
    string Code,
    string Channel,
    string LanguageCode,
    string? SubjectTemplate,
    string BodyTemplate);
