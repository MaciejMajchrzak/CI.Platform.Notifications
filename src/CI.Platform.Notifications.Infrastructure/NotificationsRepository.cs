using CI.Kernel;
using CI.Platform.Notifications.Core;
using CI.Platform.Notifications.Core.DTOs;
using CI.Platform.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CI.Platform.Notifications.Infrastructure;

public sealed class NotificationsRepository(NotificationsDbContext db) : INotificationsRepository
{
    // ── Logs ──────────────────────────────────────────────────────────────────

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

    // ── Definitions & templates ───────────────────────────────────────────────

    public async Task<NotificationDefinition?> FindDefinitionAsync(string code, CancellationToken ct = default)
        => await db.NotificationDefinitions
            .Include(x => x.Templates)
            .FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<IReadOnlyList<NotificationDefinitionDto>> ListDefinitionsAsync(CancellationToken ct = default)
        => await db.NotificationDefinitions
            .OrderBy(x => x.Code)
            .Select(x => new NotificationDefinitionDto(x.Id, x.Code, x.Name, x.DefaultChannels, x.IsSystem))
            .ToListAsync(ct);

    public async Task<NotificationTemplate?> FindTemplateAsync(string code, string channel, string languageCode, CancellationToken ct = default)
        => await db.NotificationTemplates
            .FirstOrDefaultAsync(x => x.Code == code && x.Channel == channel && x.LanguageCode == languageCode, ct);

    public async Task<IReadOnlyList<NotificationTemplateDto>> ListTemplatesAsync(string code, CancellationToken ct = default)
        => await db.NotificationTemplates
            .Where(x => x.Code == code)
            .OrderBy(x => x.Channel).ThenBy(x => x.LanguageCode)
            .Select(x => new NotificationTemplateDto(
                x.Id, x.DefinitionId, x.Code, x.Channel, x.LanguageCode, x.SubjectTemplate, x.BodyTemplate))
            .ToListAsync(ct);

    public async Task UpsertTemplateAsync(NotificationTemplate template, CancellationToken ct = default)
        => await db.NotificationTemplates.AddAsync(template, ct);

    // ── Inbox ─────────────────────────────────────────────────────────────────

    public async Task<PagedResult<NotificationInboxDto>> ListInboxAsync(
        Guid tenantId, string userId, int page, int pageSize, bool? unreadOnly, CancellationToken ct = default)
    {
        var query = db.NotificationInbox.Where(x => x.TenantId == tenantId && x.UserId == userId);
        if (unreadOnly == true) query = query.Where(x => !x.IsRead);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationInboxDto(x.Id, x.Code, x.Title, x.Body, x.IsRead, x.DeepLinkUrl, x.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<NotificationInboxDto>(items, page, pageSize, total);
    }

    public async Task<NotificationInbox?> FindInboxItemAsync(Guid id, Guid tenantId, string userId, CancellationToken ct = default)
        => await db.NotificationInbox.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.UserId == userId, ct);

    public async Task AddInboxItemAsync(NotificationInbox item, CancellationToken ct = default)
        => await db.NotificationInbox.AddAsync(item, ct);

    // ── Processed events ──────────────────────────────────────────────────────

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
