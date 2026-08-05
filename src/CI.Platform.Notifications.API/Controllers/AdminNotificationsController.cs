using CI.Kernel;
using CI.Platform.Notifications.Core.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CI.Platform.Notifications.API.Controllers;

[ApiController]
[Route("api/admin/notifications")]
[Authorize]
[AllowWithoutModule]
public sealed class AdminNotificationsController(ICommandBus commandBus) : ControllerBase
{
    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions(CancellationToken ct)
    {
        var result = await commandBus.SendAsync(new GetDefinitionsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(500);
    }

    [HttpGet("definitions/{code}/templates")]
    public async Task<IActionResult> GetTemplates(string code, CancellationToken ct)
    {
        var result = await commandBus.SendAsync(new GetTemplatesQuery(code), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPut("definitions/{code}/templates")]
    public async Task<IActionResult> UpsertTemplate(string code, [FromBody] UpsertTemplateRequest req, CancellationToken ct)
    {
        var result = await commandBus.SendAsync(
            new UpsertTemplateCommand(code, req.Channel, req.LanguageCode, req.SubjectTemplate, req.BodyTemplate), ct);
        return result.IsSuccess
            ? Ok(new { id = result.Value })
            : result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound() : BadRequest(new { error = result.ErrorCode });
    }
}

public sealed record UpsertTemplateRequest(
    string Channel,
    string LanguageCode,
    string? SubjectTemplate,
    string BodyTemplate);
