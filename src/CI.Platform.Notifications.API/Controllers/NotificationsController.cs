using CI.Kernel;
using CI.Platform.Notifications.Core.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CI.Platform.Notifications.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
[AllowWithoutModule]
public sealed class NotificationsController(ICommandBus commandBus) : ControllerBase
{
    // ── Log-based send (internal / service-to-service) ────────────────────────

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest req, CancellationToken ct)
    {
        var result = await commandBus.SendAsync(
            new SendNotificationCommand(
                req.TenantId, req.Channel, req.Recipient,
                req.TemplateKey, req.TemplateDataJson, req.IdempotencyKey), ct);
        return result.IsSuccess ? Ok(new { id = result.Value }) : Conflict(new { error = result.ErrorCode });
    }

    // ── Typed send (template-based) ───────────────────────────────────────────

    [HttpPost("send")]
    public async Task<IActionResult> SendTyped([FromBody] SendTypedNotificationRequest req, CancellationToken ct)
    {
        var result = await commandBus.SendAsync(
            new SendTypedNotificationCommand(
                req.Code, req.TenantId, req.UserId,
                req.RecipientEmail, req.Language,
                req.Data, req.IdempotencyKey), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.ErrorCode });
    }

    // ── Inbox ─────────────────────────────────────────────────────────────────

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(
        [FromQuery] Guid tenantId,
        [FromQuery] string userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? unreadOnly = null,
        CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new GetInboxQuery(tenantId, userId, page, pageSize, unreadOnly), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest();
    }

    [HttpPost("inbox/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid id, [FromBody] MarkReadRequest req, CancellationToken ct)
    {
        var result = await commandBus.SendAsync(new MarkInboxReadCommand(id, req.TenantId, req.UserId), ct);
        return result.IsSuccess ? NoContent() : result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound() : BadRequest();
    }

    // ── Log queries ───────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId, CancellationToken ct)
    {
        var result = await commandBus.SendAsync(new GetNotificationLogQuery(id, tenantId), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? channel = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(
            new ListNotificationLogsQuery(tenantId, page, pageSize, channel, status), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest();
    }
}

public sealed record SendNotificationRequest(
    Guid TenantId,
    string Channel,
    string Recipient,
    string TemplateKey,
    string TemplateDataJson,
    string? IdempotencyKey);

public sealed record SendTypedNotificationRequest(
    string Code,
    Guid TenantId,
    string? UserId,
    string RecipientEmail,
    string Language,
    Dictionary<string, object?> Data,
    string? IdempotencyKey = null);

public sealed record MarkReadRequest(Guid TenantId, string UserId);
