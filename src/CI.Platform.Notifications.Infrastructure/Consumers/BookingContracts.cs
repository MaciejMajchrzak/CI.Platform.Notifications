using CI.Kernel;

// Mirror contracts — same namespace as publisher so MassTransit routes correctly.
// Keep in sync with CI.Module.Booking.Domain.Events.BookingEvents

namespace CI.Module.Booking.Domain.Events
{
    public record AppointmentBookedEvent(
        Guid AppointmentId, Guid TenantId, Guid ServiceId, Guid CustomerId, DateTimeOffset StartAt,
        string CustomerEmail = "", string CustomerName = "", string ServiceName = "") : IEvent;
    public record AppointmentConfirmedEvent(
        Guid AppointmentId, Guid TenantId, DateTimeOffset StartAt,
        string CustomerEmail = "", string CustomerName = "", string ServiceName = "") : IEvent;
    public record AppointmentCancelledEvent(
        Guid AppointmentId, Guid TenantId, string CancelReason,
        string CustomerEmail = "", string CustomerName = "") : IEvent;
}
