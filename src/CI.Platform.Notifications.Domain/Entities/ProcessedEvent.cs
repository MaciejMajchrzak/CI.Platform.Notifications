using CI.Kernel;
namespace CI.Platform.Notifications.Domain.Entities;

/// <summary>
/// Per-module idempotency guard table — inherits CI.Kernel.ProcessedEvent base class.
/// Mapped to a separate "NotificationsProcessedEvents" table by EF configuration.
/// </summary>
public sealed class NotificationsProcessedEvent : ProcessedEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
