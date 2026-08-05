namespace CI.Platform.Notifications.Domain.Entities;

public sealed class NotificationTemplate
{
    public Guid    Id              { get; init; } = Guid.NewGuid();
    public Guid    DefinitionId    { get; init; }
    public string  Code            { get; init; } = default!;
    public string  Channel         { get; init; } = default!;
    public string  LanguageCode    { get; init; } = default!;
    public string? SubjectTemplate { get; set; }
    public string  BodyTemplate    { get; set; }  = default!;
}
