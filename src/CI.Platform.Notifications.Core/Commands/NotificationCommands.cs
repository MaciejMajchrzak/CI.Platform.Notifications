using CI.Kernel;
using CI.Platform.Notifications.Core.DTOs;
namespace CI.Platform.Notifications.Core.Commands;

// ── Log-based send (existing low-level command) ───────────────────────────────

public sealed record SendNotificationCommand(
    Guid TenantId,
    string Channel,
    string Recipient,
    string TemplateKey,
    string TemplateDataJson,
    string? IdempotencyKey) : ICommand<Guid>;

// ── Typed send (resolves definition, renders template, dispatches) ─────────────

public sealed record SendTypedNotificationCommand(
    string Code,
    Guid TenantId,
    string? UserId,
    string RecipientEmail,
    string Language,
    Dictionary<string, object?> Data,
    string? IdempotencyKey = null) : ICommand;

// ── Inbox ─────────────────────────────────────────────────────────────────────

public sealed record GetInboxQuery(
    Guid TenantId,
    string UserId,
    int Page,
    int PageSize,
    bool? UnreadOnly = null) : ICommand<PagedResult<NotificationInboxDto>>;

public sealed record MarkInboxReadCommand(
    Guid ItemId,
    Guid TenantId,
    string UserId) : ICommand;

// ── Definitions & templates (admin) ───────────────────────────────────────────

public sealed record GetDefinitionsQuery : ICommand<IReadOnlyList<NotificationDefinitionDto>>;

public sealed record GetTemplatesQuery(string Code) : ICommand<IReadOnlyList<NotificationTemplateDto>>;

public sealed record UpsertTemplateCommand(
    string Code,
    string Channel,
    string LanguageCode,
    string? SubjectTemplate,
    string BodyTemplate) : ICommand<Guid>;

// ── Log queries (existing) ────────────────────────────────────────────────────

public sealed record GetNotificationLogQuery(
    Guid LogId,
    Guid TenantId) : ICommand<NotificationLogDto>;

public sealed record ListNotificationLogsQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    string? Channel,
    string? Status) : ICommand<PagedResult<NotificationLogDto>>;
