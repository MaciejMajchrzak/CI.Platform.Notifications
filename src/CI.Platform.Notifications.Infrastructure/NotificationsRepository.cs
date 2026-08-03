using CI.Kernel;
using CI.Platform.Notifications.Core;
using CI.Platform.Notifications.Core.DTOs;
using CI.Platform.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CI.Platform.Notifications.Infrastructure;

public sealed class NotificationsRepository(NotificationsDbContext db) : INotificationsRepository
{
    public async Task<NotificationLog?> FindLogAsync(Guid logId, Guid tenantId, CancellationToken ct = default)
        => await db.NotificationLogs.FirstOrDefaultAsync(x => x.Id == logId && x.TenantId == tenantId, ct);

    public async Task<NotificationLog?> FindByIdempotencyKeyAsync(Guid tenantId, string key, CancellationToken ct = default)
        => await db.NotificationLogs.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IdempotencyKey == key, ct);

    public async Task<PagedResult<NotificationLogDto>> ListLogsAsync(
        Guid tenantId, int page, int pageSize, string? channel, string? status, CancellationToken ct = default)
    {
        var query = db.NotificationLogs.Where(x => x.TenantId == tenantId);
        if (channel is not null) query = query.Where(x => x.Channel == channel);
        if (status is not null)  query = query.Where(x => x.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationLogDto(
                x.Id, x.TenantId, x.Channel, x.Recipient, x.TemplateKey,
                x.Status, x.SentAt, x.FailureReason, x.IdempotencyKey, x.CreatedAt, x.RowVersion))
            .ToListAsync(ct);

        return new PagedResult<NotificationLogDto>(items, page, pageSize, total);
    }

    public async Task AddLogAsync(NotificationLog log, CancellationToken ct = default)
        => await db.NotificationLogs.AddAsync(log, ct);

    public async Task<bool> IsEventProcessedAsync(Guid messageId, CancellationToken ct = default)
        => await db.ProcessedEvents.AnyAsync(x => x.MessageId == messageId, ct);

    public async Task MarkEventProcessedAsync(Guid messageId, CancellationToken ct = default)
        => await db.ProcessedEvents.AddAsync(new NotificationsProcessedEvent { MessageId = messageId }, ct);

    public async Task<Result> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ErrorCodes.ROWVERSION_CONFLICT);
        }
    }
}
