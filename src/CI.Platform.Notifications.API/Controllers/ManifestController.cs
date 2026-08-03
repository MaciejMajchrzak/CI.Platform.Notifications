using CI.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CI.Platform.Notifications.API.Controllers;

[ApiController]
[Route("meta")]
[AllowAnonymous]
[AllowWithoutModule]
public sealed class ManifestController(IModuleManifest manifest) : ControllerBase
{
    [HttpGet("manifest")]
    public IActionResult GetManifest() => Ok(manifest.Describe());
}
