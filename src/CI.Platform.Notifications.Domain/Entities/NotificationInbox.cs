using CI.Kernel;
namespace CI.Platform.Notifications.Domain.Entities;

public sealed class NotificationInbox : BaseEntity
{
    public string  UserId      { get; init; } = default!;
    public string  Code        { get; init; } = default!;
    public string  Title       { get; init; } = default!;
    public string  Body        { get; init; } = default!;
    public bool    IsRead      { get; set; }
    public string? DeepLinkUrl { get; set; }
}
