using CI.Kernel;
using CI.Platform.Notifications.Core.DTOs;
using CI.Platform.Notifications.Domain.Entities;
namespace CI.Platform.Notifications.Core;

public interface INotificationsRepository
{
    Task<NotificationLog?> FindLogAsync(Guid logId, Guid tenantId, CancellationToken ct = default);
    Task<NotificationLog?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task<PagedResult<NotificationLogDto>> ListLogsAsync(Guid tenantId, int page, int pageSize, string? channel, string? status, CancellationToken ct = default);
    Task AddLogAsync(NotificationLog log, CancellationToken ct = default);
    Task<bool> IsEventProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkEventProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task<Result> SaveChangesAsync(CancellationToken ct = default);
}
