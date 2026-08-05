using CI.Kernel;

// Mirror contracts — same namespace as publisher so MassTransit routes correctly.
// Keep in sync with CI.Platform.Users.Domain.Events.UserEvents

namespace CI.Platform.Users.Domain.Events
{
    public record UserInvitedEvent(Guid TenantId, string Email, Guid Token) : IEvent;
}
