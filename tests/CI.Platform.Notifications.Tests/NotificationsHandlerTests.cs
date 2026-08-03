using CI.Kernel;
using CI.Platform.Notifications.Core.Commands;
using CI.Platform.Notifications.Core.Handlers;
using CI.Platform.Notifications.Domain.Entities;
using CI.Platform.Notifications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CI.Platform.Notifications.Tests;

public sealed class NotificationsHandlerTests : IDisposable
{
    private static readonly Guid TenantA = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = new("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly NotificationsDbContext  _db;
    private readonly NotificationsRepository _repo;

    public NotificationsHandlerTests()
    {
        var opts = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db   = new NotificationsDbContext(opts);
        _repo = new NotificationsRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Send ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_creates_log_and_returns_id()
    {
        var handler = new SendNotificationHandler(_repo);
        var result  = await handler.HandleAsync(
            new SendNotificationCommand(TenantA, "email", "user@example.com", "welcome", "{}", null), default);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
    }

    [Fact]
    public async Task Send_sets_Status_Sent()
    {
        var handler = new SendNotificationHandler(_repo);
        var result  = await handler.HandleAsync(
            new SendNotificationCommand(TenantA, "email", "u@e.com", "reset-password", "{}", null), default);

        var log = await _db.NotificationLogs.FindAsync(result.Value);
        Assert.Equal(NotificationStatus.Sent, log!.Status);
    }

    [Fact]
    public async Task Send_sets_SentAt()
    {
        var before  = DateTimeOffset.UtcNow.AddSeconds(-1);
        var handler = new SendNotificationHandler(_repo);
        var result  = await handler.HandleAsync(
            new SendNotificationCommand(TenantA, "sms", "+48000000000", "otp", "{}", null), default);

        var log = await _db.NotificationLogs.FindAsync(result.Value);
        Assert.True(log!.SentAt > before);
    }

    [Fact]
    public async Task Send_stores_channel_recipient_templateKey()
    {
        var handler = new SendNotificationHandler(_repo);
        var result  = await handler.HandleAsync(
            new SendNotificationCommand(TenantA, "push", "device-token-xyz", "promo", "{\"key\":1}", null), default);

        var log = await _db.NotificationLogs.FindAsync(result.Value);
        Assert.Equal("push",             log!.Channel);
        Assert.Equal("device-token-xyz", log.Recipient);
        Assert.Equal("promo",            log.TemplateKey);
    }

    // ── Idempotency ───────────────────────────────────────────────────────

    [Fact]
    public async Task Send_idempotency_key_deduplicates()
    {
        var handler = new SendNotificationHandler(_repo);
        var cmd = new SendNotificationCommand(TenantA, "email", "a@b.com", "tpl", "{}", "idem-key-1");

        var first  = await handler.HandleAsync(cmd, default);
        var second = await handler.HandleAsync(cmd, default);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);

        var count = _db.NotificationLogs.Count(x => x.IdempotencyKey == "idem-key-1");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Send_different_idempotency_keys_create_separate_logs()
    {
        var handler = new SendNotificationHandler(_repo);
        var r1 = await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", "a@b.com", "tpl", "{}", "key-A"), default);
        var r2 = await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", "a@b.com", "tpl", "{}", "key-B"), default);

        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public async Task Send_null_idempotency_key_never_deduplicates()
    {
        var handler = new SendNotificationHandler(_repo);
        var cmd = new SendNotificationCommand(TenantA, "email", "a@b.com", "tpl", "{}", null);

        var r1 = await handler.HandleAsync(cmd, default);
        var r2 = await handler.HandleAsync(cmd, default);

        Assert.NotEqual(r1.Value, r2.Value);
    }

    // ── GetLog ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLog_returns_dto()
    {
        var sendHandler = new SendNotificationHandler(_repo);
        var sendResult  = await sendHandler.HandleAsync(
            new SendNotificationCommand(TenantA, "email", "x@y.com", "verify", "{}", null), default);

        var getHandler = new GetNotificationLogHandler(_repo);
        var result     = await getHandler.HandleAsync(new GetNotificationLogQuery(sendResult.Value, TenantA), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(sendResult.Value, result.Value!.Id);
        Assert.Equal("email",   result.Value.Channel);
        Assert.Equal("verify",  result.Value.TemplateKey);
        Assert.Equal(TenantA,   result.Value.TenantId);
    }

    [Fact]
    public async Task GetLog_wrong_tenant_returns_NOT_FOUND()
    {
        var sendHandler = new SendNotificationHandler(_repo);
        var sendResult  = await sendHandler.HandleAsync(
            new SendNotificationCommand(TenantA, "email", "a@b.com", "tpl", "{}", null), default);

        var getHandler = new GetNotificationLogHandler(_repo);
        var result     = await getHandler.HandleAsync(new GetNotificationLogQuery(sendResult.Value, TenantB), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    public async Task GetLog_unknown_id_returns_NOT_FOUND()
    {
        var handler = new GetNotificationLogHandler(_repo);
        var result  = await handler.HandleAsync(new GetNotificationLogQuery(Guid.NewGuid(), TenantA), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── ListLogs ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListLogs_tenant_filter_isolates_tenants()
    {
        var handler = new SendNotificationHandler(_repo);
        await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", "a@a.com", "t1", "{}", null), default);
        await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", "b@a.com", "t2", "{}", null), default);
        await handler.HandleAsync(new SendNotificationCommand(TenantB, "email", "c@b.com", "t3", "{}", null), default);

        var listHandler = new ListNotificationLogsHandler(_repo);
        var resultA = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantA, 1, 20, null, null), default);
        var resultB = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantB, 1, 20, null, null), default);

        Assert.True(resultA.IsSuccess);
        Assert.Equal(2, resultA.Value!.TotalCount);
        Assert.Equal(1, resultB.Value!.TotalCount);
    }

    [Fact]
    public async Task ListLogs_channel_filter()
    {
        var handler = new SendNotificationHandler(_repo);
        await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", "a@a.com", "t1", "{}", null), default);
        await handler.HandleAsync(new SendNotificationCommand(TenantA, "sms",   "+48000",  "t2", "{}", null), default);
        await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", "b@a.com", "t3", "{}", null), default);

        var listHandler  = new ListNotificationLogsHandler(_repo);
        var emailResult  = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantA, 1, 20, "email", null), default);
        var smsResult    = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantA, 1, 20, "sms",   null), default);

        Assert.Equal(2, emailResult.Value!.TotalCount);
        Assert.Equal(1, smsResult.Value!.TotalCount);
    }

    [Fact]
    public async Task ListLogs_status_filter()
    {
        // SendNotificationHandler always marks Status=Sent, so we seed a Failed entry manually
        var log = new NotificationLog
        {
            TenantId    = TenantA,
            Channel     = "email",
            Recipient   = "f@a.com",
            TemplateKey = "fail-tpl",
            Status      = NotificationStatus.Failed,
            FailureReason = "SMTP error",
        };
        await _db.NotificationLogs.AddAsync(log);
        await _db.SaveChangesAsync();

        var handler = new SendNotificationHandler(_repo);
        await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", "ok@a.com", "ok-tpl", "{}", null), default);

        var listHandler  = new ListNotificationLogsHandler(_repo);
        var sentResult   = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantA, 1, 20, null, NotificationStatus.Sent),   default);
        var failedResult = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantA, 1, 20, null, NotificationStatus.Failed), default);

        Assert.Equal(1, sentResult.Value!.TotalCount);
        Assert.Equal(1, failedResult.Value!.TotalCount);
    }

    [Fact]
    public async Task ListLogs_pagination()
    {
        var handler = new SendNotificationHandler(_repo);
        for (var i = 0; i < 5; i++)
            await handler.HandleAsync(new SendNotificationCommand(TenantA, "email", $"u{i}@a.com", "tpl", "{}", null), default);

        var listHandler = new ListNotificationLogsHandler(_repo);
        var page1 = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantA, 1, 3, null, null), default);
        var page2 = await listHandler.HandleAsync(new ListNotificationLogsQuery(TenantA, 2, 3, null, null), default);

        Assert.Equal(5, page1.Value!.TotalCount);
        Assert.Equal(3, page1.Value.Items.Count);
        Assert.Equal(2, page2.Value!.Items.Count);
        Assert.True(page1.Value.HasNext);
        Assert.False(page2.Value.HasNext);
    }

    [Fact]
    public async Task ListLogs_empty_tenant_returns_zero()
    {
        var handler = new ListNotificationLogsHandler(_repo);
        var result  = await handler.HandleAsync(new ListNotificationLogsQuery(Guid.NewGuid(), 1, 20, null, null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalCount);
        Assert.Empty(result.Value.Items);
    }

    // ── ProcessedEvent ────────────────────────────────────────────────────

    [Fact]
    public async Task IsEventProcessed_returns_false_for_new_messageId()
    {
        var processed = await _repo.IsEventProcessedAsync(Guid.NewGuid());
        Assert.False(processed);
    }

    [Fact]
    public async Task MarkEventProcessed_then_IsEventProcessed_returns_true()
    {
        var msgId  = Guid.NewGuid();
        await _repo.MarkEventProcessedAsync(msgId);
        await _repo.SaveChangesAsync();

        var processed = await _repo.IsEventProcessedAsync(msgId);
        Assert.True(processed);
    }

    // ── DTO mapping ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetLog_dto_has_correct_channel_and_recipient()
    {
        var sendHandler = new SendNotificationHandler(_repo);
        var sendResult  = await sendHandler.HandleAsync(
            new SendNotificationCommand(TenantA, "push", "device-999", "offer", "{}", "idem-dto-test"), default);

        var getHandler = new GetNotificationLogHandler(_repo);
        var dto        = (await getHandler.HandleAsync(new GetNotificationLogQuery(sendResult.Value, TenantA), default)).Value!;

        Assert.Equal("push",        dto.Channel);
        Assert.Equal("device-999",  dto.Recipient);
        Assert.Equal("offer",       dto.TemplateKey);
        Assert.Equal("idem-dto-test", dto.IdempotencyKey);
        Assert.Equal(NotificationStatus.Sent, dto.Status);
        Assert.NotNull(dto.SentAt);
    }
}
