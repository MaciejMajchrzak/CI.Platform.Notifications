using CI.Kernel;
using CI.Platform.Notifications.Core.Commands;
using CI.Platform.Notifications.Core.DTOs;
using CI.Platform.Notifications.Domain.Entities;
namespace CI.Platform.Notifications.Core.Handlers;

[NoEvent("delivery service — notification log is the record of side effect")]
public sealed class SendNotificationHandler(INotificationsRepository repo)
    : ICommandHandler<SendNotificationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(SendNotificationCommand cmd, CancellationToken ct)
    {
        if (cmd.IdempotencyKey is not null)
        {
            var existing = await repo.FindByIdempotencyKeyAsync(cmd.TenantId, cmd.IdempotencyKey, ct);
            if (existing is not null)
                return Result<Guid>.Success(existing.Id);
        }

        var log = new NotificationLog
        {
            TenantId       = cmd.TenantId,
            Channel        = cmd.Channel,
            Recipient      = cmd.Recipient,
            TemplateKey    = cmd.TemplateKey,
            Status         = NotificationStatus.Sent,
            SentAt         = DateTimeOffset.UtcNow,
            IdempotencyKey = cmd.IdempotencyKey,
        };

        await repo.AddLogAsync(log, ct);
        var result = await repo.SaveChangesAsync(ct);
        return result.IsSuccess ? Result<Guid>.Success(log.Id) : Result<Guid>.Failure(result.ErrorCode!);
    }
}

[NoEvent("query — read only")]
public sealed class GetNotificationLogHandler(INotificationsRepository repo)
    : ICommandHandler<GetNotificationLogQuery, NotificationLogDto>
{
    public async Task<Result<NotificationLogDto>> HandleAsync(GetNotificationLogQuery query, CancellationToken ct)
    {
        var log = await repo.FindLogAsync(query.LogId, query.TenantId, ct);
        if (log is null) return Result<NotificationLogDto>.Failure(ErrorCodes.NOT_FOUND);
        return Result<NotificationLogDto>.Success(ToDto(log));
    }

    private static NotificationLogDto ToDto(NotificationLog l) => new(
        l.Id, l.TenantId, l.Channel, l.Recipient, l.TemplateKey,
        l.Status, l.SentAt, l.FailureReason, l.IdempotencyKey, l.CreatedAt, l.RowVersion);
}

[NoEvent("query — read only")]
public sealed class ListNotificationLogsHandler(INotificationsRepository repo)
    : ICommandHandler<ListNotificationLogsQuery, PagedResult<NotificationLogDto>>
{
    public async Task<Result<PagedResult<NotificationLogDto>>> HandleAsync(ListNotificationLogsQuery query, CancellationToken ct)
    {
        var result = await repo.ListLogsAsync(query.TenantId, query.Page, query.PageSize, query.Channel, query.Status, ct);
        return Result<PagedResult<NotificationLogDto>>.Success(result);
    }
}
