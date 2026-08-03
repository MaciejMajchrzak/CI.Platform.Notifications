using CI.Kernel;
using CI.Platform.Notifications.Core.DTOs;
namespace CI.Platform.Notifications.Core.Commands;

public sealed record SendNotificationCommand(
    Guid TenantId,
    string Channel,
    string Recipient,
    string TemplateKey,
    string TemplateDataJson,
    string? IdempotencyKey) : ICommand<Guid>;

public sealed record GetNotificationLogQuery(
    Guid LogId,
    Guid TenantId) : ICommand<NotificationLogDto>;

public sealed record ListNotificationLogsQuery(
    Guid TenantId,
    int Page,
    int PageSize,
    string? Channel,
    string? Status) : ICommand<PagedResult<NotificationLogDto>>;
