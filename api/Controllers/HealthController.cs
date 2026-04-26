using Maestro.Api.Models.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Maestro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(SystemHealth health) : ControllerBase
{
    [HttpGet]
    public ActionResult<SystemHealth> Get() => health;
}
