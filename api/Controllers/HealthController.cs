using Microsoft.AspNetCore.Mvc;

namespace Maestro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", name = "Maestro" });
}
