using CI.Module.Booking.Domain.Events;
using CI.Platform.Notifications.Infrastructure;
using CI.Platform.Notifications.Infrastructure.Consumers;
using CI.Platform.Users.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CI.Platform.Notifications.Tests;

// Tracking sender that records what was sent
internal sealed class TrackingNotificationSender : CI.Platform.Notifications.Core.INotificationSender
{
    public List<(string Code, Guid TenantId, string Email, Dictionary<string, object?> Data)> Sent { get; } = [];

    public Task SendAsync(string code, Guid tenantId, string? userId, string recipientEmail,
        string language, Dictionary<string, object?> data, string? idempotencyKey = null, CancellationToken ct = default)
    {
        Sent.Add((code, tenantId, recipientEmail, data));
        return Task.CompletedTask;
    }
}

internal static class ConsumerTestDb
{
    public static (NotificationsDbContext db, NotificationsRepository repo) Build()
    {
        var opts = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db   = new NotificationsDbContext(opts);
        var repo = new NotificationsRepository(db);
        return (db, repo);
    }
}

public sealed class AppointmentConfirmedNotificationConsumerTests : IDisposable
{
    private static readonly Guid TenantId = new("aaaaaaaa-1111-0000-0000-000000000001");
    private readonly NotificationsDbContext         _db;
    private readonly NotificationsRepository        _repo;
    private readonly TrackingNotificationSender     _sender = new();
    private readonly AppointmentConfirmedNotificationConsumer _consumer;

    public AppointmentConfirmedNotificationConsumerTests()
    {
        (_db, _repo) = ConsumerTestDb.Build();
        _consumer    = new AppointmentConfirmedNotificationConsumer(_repo, _sender);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_sends_booking_confirmed_notification()
    {
        var apptId = Guid.NewGuid();
        var msg    = new AppointmentConfirmedEvent(apptId, TenantId, DateTimeOffset.UtcNow,
            "customer@example.com", "Jane Doe", "Haircut");

        await _consumer.HandleAsync(msg, null);

        Assert.Single(_sender.Sent);
        var (code, tid, email, data) = _sender.Sent[0];
        Assert.Equal("booking.confirmed",    code);
        Assert.Equal(TenantId,               tid);
        Assert.Equal("customer@example.com", email);
        Assert.Equal("Jane Doe",             data["customerName"]?.ToString());
        Assert.Equal("Haircut",              data["serviceName"]?.ToString());
    }

    [Fact]
    public async Task Handle_skips_when_no_email()
    {
        var msg = new AppointmentConfirmedEvent(Guid.NewGuid(), TenantId, DateTimeOffset.UtcNow,
            "", "Jane Doe", "Haircut");

        await _consumer.HandleAsync(msg, null);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_is_idempotent()
    {
        var apptId = Guid.NewGuid();
        var msgId  = Guid.NewGuid();
        var msg    = new AppointmentConfirmedEvent(apptId, TenantId, DateTimeOffset.UtcNow,
            "u@e.com", "Jane", "Haircut");

        await _consumer.HandleAsync(msg, msgId);
        await _consumer.HandleAsync(msg, msgId);

        Assert.Single(_sender.Sent);
    }
}

public sealed class AppointmentCancelledNotificationConsumerTests : IDisposable
{
    private static readonly Guid TenantId = new("aaaaaaaa-2222-0000-0000-000000000001");
    private readonly NotificationsDbContext         _db;
    private readonly NotificationsRepository        _repo;
    private readonly TrackingNotificationSender     _sender = new();
    private readonly AppointmentCancelledNotificationConsumer _consumer;

    public AppointmentCancelledNotificationConsumerTests()
    {
        (_db, _repo) = ConsumerTestDb.Build();
        _consumer    = new AppointmentCancelledNotificationConsumer(_repo, _sender);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_sends_booking_cancelled_notification()
    {
        var apptId = Guid.NewGuid();
        var msg    = new AppointmentCancelledEvent(apptId, TenantId, "Customer request",
            "customer@example.com", "Jane Doe");

        await _consumer.HandleAsync(msg, null);

        Assert.Single(_sender.Sent);
        var (code, tid, email, _) = _sender.Sent[0];
        Assert.Equal("booking.cancelled",    code);
        Assert.Equal(TenantId,               tid);
        Assert.Equal("customer@example.com", email);
    }

    [Fact]
    public async Task Handle_skips_when_no_email()
    {
        var msg = new AppointmentCancelledEvent(Guid.NewGuid(), TenantId, "reason", "", "Jane");

        await _consumer.HandleAsync(msg, null);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Handle_is_idempotent()
    {
        var apptId = Guid.NewGuid();
        var msgId  = Guid.NewGuid();
        var msg    = new AppointmentCancelledEvent(apptId, TenantId, "reason", "u@e.com", "Jane");

        await _consumer.HandleAsync(msg, msgId);
        await _consumer.HandleAsync(msg, msgId);

        Assert.Single(_sender.Sent);
    }
}

public sealed class UserInvitedNotificationConsumerTests : IDisposable
{
    private static readonly Guid TenantId = new("aaaaaaaa-3333-0000-0000-000000000001");
    private readonly NotificationsDbContext      _db;
    private readonly NotificationsRepository     _repo;
    private readonly TrackingNotificationSender  _sender = new();
    private readonly UserInvitedNotificationConsumer _consumer;

    public UserInvitedNotificationConsumerTests()
    {
        (_db, _repo) = ConsumerTestDb.Build();
        _consumer    = new UserInvitedNotificationConsumer(_repo, _sender);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_sends_user_invited_notification()
    {
        var token = Guid.NewGuid();
        var msg   = new UserInvitedEvent(TenantId, "newmember@example.com", token);

        await _consumer.HandleAsync(msg, null);

        Assert.Single(_sender.Sent);
        var (code, tid, email, data) = _sender.Sent[0];
        Assert.Equal("user.invited",          code);
        Assert.Equal(TenantId,                tid);
        Assert.Equal("newmember@example.com", email);
        Assert.Contains(token.ToString(),     data["inviteLink"]?.ToString() ?? "");
    }

    [Fact]
    public async Task Handle_is_idempotent()
    {
        var token = Guid.NewGuid();
        var msgId = Guid.NewGuid();
        var msg   = new UserInvitedEvent(TenantId, "u@e.com", token);

        await _consumer.HandleAsync(msg, msgId);
        await _consumer.HandleAsync(msg, msgId);

        Assert.Single(_sender.Sent);
    }
}
