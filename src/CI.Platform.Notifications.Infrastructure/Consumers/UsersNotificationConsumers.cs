using CI.Platform.Notifications.Core;
using CI.Platform.Users.Domain.Events;
using MassTransit;

namespace CI.Platform.Notifications.Infrastructure.Consumers;

public sealed class UserInvitedNotificationConsumer(
    INotificationsRepository repo,
    INotificationSender sender)
    : IConsumer<UserInvitedEvent>
{
    public Task Consume(ConsumeContext<UserInvitedEvent> ctx) =>
        HandleAsync(ctx.Message, ctx.MessageId, ctx.CancellationToken);

    public async Task HandleAsync(UserInvitedEvent msg, Guid? messageId, CancellationToken ct = default)
    {
        var dedupKey = messageId ?? msg.Token;
        if (await repo.IsEventProcessedAsync(dedupKey, ct)) return;

        await sender.SendAsync(
            "user.invited",
            msg.TenantId,
            userId: null,
            recipientEmail: msg.Email,
            language: "en",
            data: new Dictionary<string, object?>
            {
                ["tenantName"] = string.Empty,
                ["inviteLink"] = $"/accept-invitation/{msg.Token}",
            },
            idempotencyKey: messageId?.ToString(),
            ct: ct);

        await repo.MarkEventProcessedAsync(dedupKey, ct);
        await repo.SaveChangesAsync(ct);
    }
}
