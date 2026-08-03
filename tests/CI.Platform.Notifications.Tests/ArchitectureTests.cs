using System.Reflection;
using CI.Kernel.ArchTests;
using CI.Platform.Notifications.API.Controllers;
using CI.Platform.Notifications.Core.Handlers;
using CI.Platform.Notifications.Domain.Entities;
using CI.Platform.Notifications.Infrastructure;
namespace CI.Platform.Notifications.Tests;

public sealed class NotificationsArchitectureTests : ModuleArchitectureTests
{
    protected override Assembly DomainAssembly => typeof(NotificationLog).Assembly;
    protected override Assembly CoreAssembly   => typeof(SendNotificationHandler).Assembly;
    protected override Assembly InfraAssembly  => typeof(NotificationsDbContext).Assembly;
    protected override Assembly ApiAssembly    => typeof(NotificationsController).Assembly;

    // NotificationsProcessedEvent is an idempotency guard — no TenantId, not a tenant aggregate
    protected override bool ExcludeFromBaseEntityCheck(Type t) =>
        t == typeof(NotificationsProcessedEvent);
}
