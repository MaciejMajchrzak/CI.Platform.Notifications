using CI.Kernel;
using CI.Platform.Notifications.Core.Commands;
using CI.Platform.Notifications.Core.DTOs;
using CI.Platform.Notifications.Domain.Entities;
namespace CI.Platform.Notifications.Core.Handlers;

// ── Existing log-based send ───────────────────────────────────────────────────

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

// ── Typed send (dispatches to INotificationSender) ───────────────────────────

[NoEvent("side effect is the notification itself")]
public sealed class SendTypedNotificationHandler(INotificationSender sender)
    : ICommandHandler<SendTypedNotificationCommand>
{
    public async Task<Result> HandleAsync(SendTypedNotificationCommand cmd, CancellationToken ct)
    {
        await sender.SendAsync(
            cmd.Code, cmd.TenantId, cmd.UserId,
            cmd.RecipientEmail, cmd.Language, cmd.Data,
            cmd.IdempotencyKey, ct);
        return Result.Success();
    }
}

// ── Inbox ─────────────────────────────────────────────────────────────────────

[NoEvent("query — read only")]
public sealed class GetInboxHandler(INotificationsRepository repo)
    : ICommandHandler<GetInboxQuery, PagedResult<NotificationInboxDto>>
{
    public async Task<Result<PagedResult<NotificationInboxDto>>> HandleAsync(GetInboxQuery q, CancellationToken ct)
    {
        var result = await repo.ListInboxAsync(q.TenantId, q.UserId, q.Page, q.PageSize, q.UnreadOnly, ct);
        return Result<PagedResult<NotificationInboxDto>>.Success(result);
    }
}

[NoEvent("mark read — no business event")]
public sealed class MarkInboxReadHandler(INotificationsRepository repo)
    : ICommandHandler<MarkInboxReadCommand>
{
    public async Task<Result> HandleAsync(MarkInboxReadCommand cmd, CancellationToken ct)
    {
        var item = await repo.FindInboxItemAsync(cmd.ItemId, cmd.TenantId, cmd.UserId, ct);
        if (item is null) return Result.Failure(ErrorCodes.NOT_FOUND);
        item.IsRead = true;
        return await repo.SaveChangesAsync(ct);
    }
}

// ── Definitions & templates ───────────────────────────────────────────────────

[NoEvent("query — read only")]
public sealed class GetDefinitionsHandler(INotificationsRepository repo)
    : ICommandHandler<GetDefinitionsQuery, IReadOnlyList<NotificationDefinitionDto>>
{
    public async Task<Result<IReadOnlyList<NotificationDefinitionDto>>> HandleAsync(GetDefinitionsQuery q, CancellationToken ct)
        => Result<IReadOnlyList<NotificationDefinitionDto>>.Success(await repo.ListDefinitionsAsync(ct));
}

[NoEvent("query — read only")]
public sealed class GetTemplatesHandler(INotificationsRepository repo)
    : ICommandHandler<GetTemplatesQuery, IReadOnlyList<NotificationTemplateDto>>
{
    public async Task<Result<IReadOnlyList<NotificationTemplateDto>>> HandleAsync(GetTemplatesQuery q, CancellationToken ct)
        => Result<IReadOnlyList<NotificationTemplateDto>>.Success(await repo.ListTemplatesAsync(q.Code, ct));
}

[NoEvent("admin operation — templates are config")]
public sealed class UpsertTemplateHandler(INotificationsRepository repo)
    : ICommandHandler<UpsertTemplateCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UpsertTemplateCommand cmd, CancellationToken ct)
    {
        var existing = await repo.FindTemplateAsync(cmd.Code, cmd.Channel, cmd.LanguageCode, ct);

        if (existing is not null)
        {
            existing.SubjectTemplate = cmd.SubjectTemplate;
            existing.BodyTemplate    = cmd.BodyTemplate;
            var save = await repo.SaveChangesAsync(ct);
            return save.IsSuccess ? Result<Guid>.Success(existing.Id) : Result<Guid>.Failure(save.ErrorCode!);
        }

        var def = await repo.FindDefinitionAsync(cmd.Code, ct);
        if (def is null) return Result<Guid>.Failure(ErrorCodes.NOT_FOUND);

        var template = new NotificationTemplate
        {
            DefinitionId    = def.Id,
            Code            = cmd.Code,
            Channel         = cmd.Channel,
            LanguageCode    = cmd.LanguageCode,
            SubjectTemplate = cmd.SubjectTemplate,
            BodyTemplate    = cmd.BodyTemplate,
        };
        await repo.UpsertTemplateAsync(template, ct);
        var result = await repo.SaveChangesAsync(ct);
        return result.IsSuccess ? Result<Guid>.Success(template.Id) : Result<Guid>.Failure(result.ErrorCode!);
    }
}

// ── Log queries ───────────────────────────────────────────────────────────────

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
