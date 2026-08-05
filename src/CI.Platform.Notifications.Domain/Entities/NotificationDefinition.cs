namespace CI.Platform.Notifications.Domain.Entities;

public sealed class NotificationDefinition
{
    public Guid   Id              { get; init; } = Guid.NewGuid();
    public string Code            { get; init; } = default!;
    public string Name            { get; init; } = default!;
    public int    DefaultChannels { get; set; }  // NotificationChannelFlags bitmask
    public bool   IsSystem        { get; init; }

    public List<NotificationTemplate> Templates { get; set; } = [];
}

[Flags]
public enum NotificationChannelFlags
{
    None  = 0,
    Email = 1,
    Sms   = 2,
    Push  = 4,
    InApp = 8,
}

public static class NotificationChannel
{
    public const string Email = "email";
    public const string Sms   = "sms";
    public const string Push  = "push";
    public const string InApp = "in_app";
}
