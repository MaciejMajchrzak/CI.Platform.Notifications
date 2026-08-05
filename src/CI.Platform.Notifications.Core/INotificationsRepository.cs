using CI.Kernel;
using CI.Platform.Notifications.Core.DTOs;
using CI.Platform.Notifications.Domain.Entities;
namespace CI.Platform.Notifications.Core;

public interface INotificationsRepository
{
    // Logs
    Task<NotificationLog?> FindLogAsync(Guid logId, Guid tenantId, CancellationToken ct = default);
    Task<NotificationLog?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task<PagedResult<NotificationLogDto>> ListLogsAsync(Guid tenantId, int page, int pageSize, string? channel, string? status, CancellationToken ct = default);
    Task AddLogAsync(NotificationLog log, CancellationToken ct = default);

    // Definitions & templates
    Task<NotificationDefinition?> FindDefinitionAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDefinitionDto>> ListDefinitionsAsync(CancellationToken ct = default);
    Task<NotificationTemplate?> FindTemplateAsync(string code, string channel, string languageCode, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplateDto>> ListTemplatesAsync(string code, CancellationToken ct = default);
    Task UpsertTemplateAsync(NotificationTemplate template, CancellationToken ct = default);

    // Inbox
    Task<PagedResult<NotificationInboxDto>> ListInboxAsync(Guid tenantId, string userId, int page, int pageSize, bool? unreadOnly, CancellationToken ct = default);
    Task<NotificationInbox?> FindInboxItemAsync(Guid id, Guid tenantId, string userId, CancellationToken ct = default);
    Task AddInboxItemAsync(NotificationInbox item, CancellationToken ct = default);

    // Events
    Task<bool> IsEventProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkEventProcessedAsync(Guid messageId, CancellationToken ct = default);

    Task<Result> SaveChangesAsync(CancellationToken ct = default);
}
