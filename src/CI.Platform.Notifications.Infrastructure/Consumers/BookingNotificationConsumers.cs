using CI.Module.Booking.Domain.Events;
using CI.Platform.Notifications.Core;
using MassTransit;

namespace CI.Platform.Notifications.Infrastructure.Consumers;

public sealed class AppointmentConfirmedNotificationConsumer(
    INotificationsRepository repo,
    INotificationSender sender)
    : IConsumer<AppointmentConfirmedEvent>
{
    public Task Consume(ConsumeContext<AppointmentConfirmedEvent> ctx) =>
        HandleAsync(ctx.Message, ctx.MessageId, ctx.CancellationToken);

    public async Task HandleAsync(AppointmentConfirmedEvent msg, Guid? messageId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(msg.CustomerEmail)) return;
        if (await repo.IsEventProcessedAsync(messageId ?? msg.AppointmentId, ct)) return;

        await sender.SendAsync(
            "booking.confirmed",
            msg.TenantId,
            userId: null,
            recipientEmail: msg.CustomerEmail,
            language: "en",
            data: new Dictionary<string, object?>
            {
                ["customerName"] = msg.CustomerName,
                ["serviceName"]  = msg.ServiceName,
                ["dateTime"]     = msg.StartAt.ToString("f"),
                ["businessName"] = string.Empty,
                ["bookingRef"]   = msg.AppointmentId.ToString("N")[..8].ToUpper(),
            },
            idempotencyKey: messageId?.ToString(),
            ct: ct);

        await repo.MarkEventProcessedAsync(messageId ?? msg.AppointmentId, ct);
        await repo.SaveChangesAsync(ct);
    }
}

public sealed class AppointmentCancelledNotificationConsumer(
    INotificationsRepository repo,
    INotificationSender sender)
    : IConsumer<AppointmentCancelledEvent>
{
    public Task Consume(ConsumeContext<AppointmentCancelledEvent> ctx) =>
        HandleAsync(ctx.Message, ctx.MessageId, ctx.CancellationToken);

    public async Task HandleAsync(AppointmentCancelledEvent msg, Guid? messageId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(msg.CustomerEmail)) return;
        if (await repo.IsEventProcessedAsync(messageId ?? msg.AppointmentId, ct)) return;

        await sender.SendAsync(
            "booking.cancelled",
            msg.TenantId,
            userId: null,
            recipientEmail: msg.CustomerEmail,
            language: "en",
            data: new Dictionary<string, object?>
            {
                ["customerName"] = msg.CustomerName,
                ["serviceName"]  = string.Empty,
                ["dateTime"]     = string.Empty,
            },
            idempotencyKey: messageId?.ToString(),
            ct: ct);

        await repo.MarkEventProcessedAsync(messageId ?? msg.AppointmentId, ct);
        await repo.SaveChangesAsync(ct);
    }
}
