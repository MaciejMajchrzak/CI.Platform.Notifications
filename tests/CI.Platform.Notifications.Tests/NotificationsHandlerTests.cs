using CI.Kernel;
using CI.Platform.Notifications.Core;
using CI.Platform.Notifications.Core.Commands;
using CI.Platform.Notifications.Core.Handlers;
using CI.Platform.Notifications.Domain.Entities;
using CI.Platform.Notifications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    // ── Send ──────────────────────────────────────────────────────────────────

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

    // ── Idempotency ───────────────────────────────────────────────────────────

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

    // ── GetLog ────────────────────────────────────────────────────────────────

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

    // ── ListLogs ──────────────────────────────────────────────────────────────

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
        var log = new NotificationLog
        {
            TenantId      = TenantA,
            Channel       = "email",
            Recipient     = "f@a.com",
            TemplateKey   = "fail-tpl",
            Status        = NotificationStatus.Failed,
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

    // ── ProcessedEvent ────────────────────────────────────────────────────────

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

    // ── DTO mapping ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLog_dto_has_correct_channel_and_recipient()
    {
        var sendHandler = new SendNotificationHandler(_repo);
        var sendResult  = await sendHandler.HandleAsync(
            new SendNotificationCommand(TenantA, "push", "device-999", "offer", "{}", "idem-dto-test"), default);

        var getHandler = new GetNotificationLogHandler(_repo);
        var dto        = (await getHandler.HandleAsync(new GetNotificationLogQuery(sendResult.Value, TenantA), default)).Value!;

        Assert.Equal("push",          dto.Channel);
        Assert.Equal("device-999",    dto.Recipient);
        Assert.Equal("offer",         dto.TemplateKey);
        Assert.Equal("idem-dto-test", dto.IdempotencyKey);
        Assert.Equal(NotificationStatus.Sent, dto.Status);
        Assert.NotNull(dto.SentAt);
    }

    // ── Inbox ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInbox_returns_items_for_user()
    {
        await _repo.AddInboxItemAsync(new NotificationInbox
        {
            TenantId = TenantA, UserId = "user-1", Code = "booking.confirmed",
            Title = "Confirmed", Body = "Your booking is confirmed",
        });
        await _repo.AddInboxItemAsync(new NotificationInbox
        {
            TenantId = TenantA, UserId = "user-2", Code = "booking.cancelled",
            Title = "Cancelled", Body = "Your booking was cancelled",
        });
        await _repo.SaveChangesAsync();

        var handler = new GetInboxHandler(_repo);
        var result  = await handler.HandleAsync(new GetInboxQuery(TenantA, "user-1", 1, 20), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
        Assert.Equal("booking.confirmed", result.Value.Items[0].Code);
    }

    [Fact]
    public async Task GetInbox_unread_filter()
    {
        await _repo.AddInboxItemAsync(new NotificationInbox
        {
            TenantId = TenantA, UserId = "u", Code = "a", Title = "T1", Body = "B1", IsRead = false,
        });
        var readItem = new NotificationInbox
        {
            TenantId = TenantA, UserId = "u", Code = "b", Title = "T2", Body = "B2", IsRead = true,
        };
        await _repo.AddInboxItemAsync(readItem);
        await _repo.SaveChangesAsync();

        var handler   = new GetInboxHandler(_repo);
        var unreadRes = await handler.HandleAsync(new GetInboxQuery(TenantA, "u", 1, 20, UnreadOnly: true), default);

        Assert.Equal(1, unreadRes.Value!.TotalCount);
        Assert.False(unreadRes.Value.Items[0].IsRead);
    }

    [Fact]
    public async Task MarkInboxRead_sets_IsRead()
    {
        var item = new NotificationInbox
        {
            TenantId = TenantA, UserId = "user-x", Code = "booking.confirmed",
            Title = "Hi", Body = "Body", IsRead = false,
        };
        await _repo.AddInboxItemAsync(item);
        await _repo.SaveChangesAsync();

        var handler = new MarkInboxReadHandler(_repo);
        var result  = await handler.HandleAsync(new MarkInboxReadCommand(item.Id, TenantA, "user-x"), default);

        Assert.True(result.IsSuccess);
        var updated = await _repo.FindInboxItemAsync(item.Id, TenantA, "user-x");
        Assert.True(updated!.IsRead);
    }

    [Fact]
    public async Task MarkInboxRead_wrong_user_returns_NOT_FOUND()
    {
        var item = new NotificationInbox
        {
            TenantId = TenantA, UserId = "owner", Code = "x", Title = "T", Body = "B",
        };
        await _repo.AddInboxItemAsync(item);
        await _repo.SaveChangesAsync();

        var handler = new MarkInboxReadHandler(_repo);
        var result  = await handler.HandleAsync(new MarkInboxReadCommand(item.Id, TenantA, "other"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── Definitions & templates ───────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_returns_seeded_list()
    {
        await _db.NotificationDefinitions.AddAsync(new NotificationDefinition
        {
            Code = "test.event", Name = "Test Event",
            DefaultChannels = (int)NotificationChannelFlags.Email, IsSystem = true,
        });
        await _db.SaveChangesAsync();

        var handler = new GetDefinitionsHandler(_repo);
        var result  = await handler.HandleAsync(new GetDefinitionsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!, d => d.Code == "test.event");
    }

    [Fact]
    public async Task UpsertTemplate_creates_new_template()
    {
        var def = new NotificationDefinition
        {
            Code = "booking.confirmed", Name = "Booking Confirmed",
            DefaultChannels = 9, IsSystem = true,
        };
        await _db.NotificationDefinitions.AddAsync(def);
        await _db.SaveChangesAsync();

        var handler = new UpsertTemplateHandler(_repo);
        var result  = await handler.HandleAsync(
            new UpsertTemplateCommand("booking.confirmed", "email", "pl", "Potwierdzenie: {{serviceName}}", "<p>Cześć {{customerName}}</p>"), default);

        Assert.True(result.IsSuccess);
        var tpl = await _repo.FindTemplateAsync("booking.confirmed", "email", "pl");
        Assert.NotNull(tpl);
        Assert.Equal("pl", tpl!.LanguageCode);
    }

    [Fact]
    public async Task UpsertTemplate_updates_existing_template()
    {
        var def = new NotificationDefinition
        {
            Code = "booking.cancelled", Name = "Cancelled",
            DefaultChannels = 9, IsSystem = true,
        };
        var tpl = new NotificationTemplate
        {
            Id = Guid.NewGuid(), DefinitionId = def.Id,
            Code = "booking.cancelled", Channel = "email", LanguageCode = "en",
            SubjectTemplate = "Old subject", BodyTemplate = "Old body",
        };
        def.Templates.Add(tpl);
        await _db.NotificationDefinitions.AddAsync(def);
        await _db.SaveChangesAsync();

        var handler = new UpsertTemplateHandler(_repo);
        await handler.HandleAsync(
            new UpsertTemplateCommand("booking.cancelled", "email", "en", "New subject", "New body"), default);

        var updated = await _repo.FindTemplateAsync("booking.cancelled", "email", "en");
        Assert.Equal("New subject", updated!.SubjectTemplate);
        Assert.Equal("New body",    updated.BodyTemplate);
    }

    [Fact]
    public async Task UpsertTemplate_unknown_code_returns_NOT_FOUND()
    {
        var handler = new UpsertTemplateHandler(_repo);
        var result  = await handler.HandleAsync(
            new UpsertTemplateCommand("does.not.exist", "email", "en", null, "Body"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── SendTyped + NotificationSender ────────────────────────────────────────

    [Fact]
    public async Task SendTyped_renders_template_and_creates_inbox_item()
    {
        var def = new NotificationDefinition
        {
            Code = "booking.confirmed", Name = "Booking Confirmed",
            DefaultChannels = (int)(NotificationChannelFlags.Email | NotificationChannelFlags.InApp),
            IsSystem = true,
        };
        def.Templates.Add(new NotificationTemplate
        {
            DefinitionId = def.Id, Code = "booking.confirmed",
            Channel = "email", LanguageCode = "en",
            SubjectTemplate = "Confirmed: {{serviceName}}",
            BodyTemplate    = "<p>Hi {{customerName}}</p>",
        });
        def.Templates.Add(new NotificationTemplate
        {
            DefinitionId = def.Id, Code = "booking.confirmed",
            Channel = "in_app", LanguageCode = "en",
            SubjectTemplate = "Booking confirmed",
            BodyTemplate    = "Your {{serviceName}} booking is confirmed",
        });
        await _db.NotificationDefinitions.AddAsync(def);
        await _db.SaveChangesAsync();

        var emailCapture = new CapturingEmailSender();
        var sender  = new NotificationSender(_repo, emailCapture, NullLogger<NotificationSender>.Instance);
        var handler = new SendTypedNotificationHandler(sender);

        var result = await handler.HandleAsync(new SendTypedNotificationCommand(
            "booking.confirmed", TenantA, "user-99", "cust@example.com", "en",
            new Dictionary<string, object?>
            {
                ["serviceName"]   = "Yoga",
                ["customerName"]  = "Anna",
            }), default);

        Assert.True(result.IsSuccess);

        // Email was sent with rendered subject
        Assert.Equal("Confirmed: Yoga",   emailCapture.LastSubject);
        Assert.Contains("Hi Anna",        emailCapture.LastBody);

        // Inbox item created
        var inbox = await _repo.ListInboxAsync(TenantA, "user-99", 1, 10, null);
        Assert.Equal(1, inbox.TotalCount);
        Assert.Equal("Your Yoga booking is confirmed", inbox.Items[0].Body);
    }

    [Fact]
    public async Task SendTyped_falls_back_to_en_template_for_unknown_language()
    {
        var def = new NotificationDefinition
        {
            Code = "booking.cancelled", Name = "Cancelled",
            DefaultChannels = (int)NotificationChannelFlags.Email, IsSystem = true,
        };
        def.Templates.Add(new NotificationTemplate
        {
            DefinitionId = def.Id, Code = "booking.cancelled",
            Channel = "email", LanguageCode = "en",
            SubjectTemplate = "Cancelled: {{serviceName}}",
            BodyTemplate    = "<p>Cancelled</p>",
        });
        await _db.NotificationDefinitions.AddAsync(def);
        await _db.SaveChangesAsync();

        var emailCapture = new CapturingEmailSender();
        var sender  = new NotificationSender(_repo, emailCapture, NullLogger<NotificationSender>.Instance);
        var handler = new SendTypedNotificationHandler(sender);

        await handler.HandleAsync(new SendTypedNotificationCommand(
            "booking.cancelled", TenantA, null, "x@y.com", "zh",
            new Dictionary<string, object?> { ["serviceName"] = "Pilates" }), default);

        // fell back to EN template
        Assert.Equal("Cancelled: Pilates", emailCapture.LastSubject);
    }

    [Fact]
    public async Task SendTyped_unknown_code_skips_gracefully()
    {
        var emailCapture = new CapturingEmailSender();
        var sender  = new NotificationSender(_repo, emailCapture, NullLogger<NotificationSender>.Instance);
        var handler = new SendTypedNotificationHandler(sender);

        var result = await handler.HandleAsync(new SendTypedNotificationCommand(
            "does.not.exist", TenantA, null, "x@y.com", "en",
            new Dictionary<string, object?>()), default);

        Assert.True(result.IsSuccess); // graceful — unknown code is not an error
        Assert.Null(emailCapture.LastSubject);
    }

    [Fact]
    public async Task SendTyped_email_failure_logs_failed_status_but_returns_success()
    {
        var def = new NotificationDefinition
        {
            Code = "invoice.overdue", Name = "Invoice Overdue",
            DefaultChannels = (int)NotificationChannelFlags.Email, IsSystem = true,
        };
        def.Templates.Add(new NotificationTemplate
        {
            DefinitionId = def.Id, Code = "invoice.overdue",
            Channel = "email", LanguageCode = "en",
            SubjectTemplate = "Overdue: {{invoiceNumber}}",
            BodyTemplate    = "<p>Pay now</p>",
        });
        await _db.NotificationDefinitions.AddAsync(def);
        await _db.SaveChangesAsync();

        var failingSender = new FailingEmailSender();
        var sender  = new NotificationSender(_repo, failingSender, NullLogger<NotificationSender>.Instance);
        var handler = new SendTypedNotificationHandler(sender);

        var result = await handler.HandleAsync(new SendTypedNotificationCommand(
            "invoice.overdue", TenantA, null, "x@y.com", "en",
            new Dictionary<string, object?> { ["invoiceNumber"] = "INV-001" }), default);

        Assert.True(result.IsSuccess); // sender failure doesn't bubble up

        var logs = await _repo.ListLogsAsync(TenantA, 1, 10, "email", NotificationStatus.Failed);
        Assert.Equal(1, logs.TotalCount);
    }
}

// ── Test doubles ──────────────────────────────────────────────────────────────

file sealed class CapturingEmailSender : IEmailSender
{
    public string? LastTo      { get; private set; }
    public string? LastSubject { get; private set; }
    public string? LastBody    { get; private set; }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        LastTo      = to;
        LastSubject = subject;
        LastBody    = htmlBody;
        return Task.CompletedTask;
    }
}

file sealed class FailingEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        => throw new InvalidOperationException("SMTP unreachable");
}
