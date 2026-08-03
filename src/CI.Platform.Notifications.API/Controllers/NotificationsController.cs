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
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest req, CancellationToken ct)
    {
        var result = await commandBus.SendAsync(
            new SendNotificationCommand(
                req.TenantId, req.Channel, req.Recipient,
                req.TemplateKey, req.TemplateDataJson, req.IdempotencyKey), ct);
        return result.IsSuccess ? Ok(new { id = result.Value }) : Conflict(new { error = result.ErrorCode });
    }

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
