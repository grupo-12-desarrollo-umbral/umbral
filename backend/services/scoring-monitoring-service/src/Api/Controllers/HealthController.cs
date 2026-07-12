using Microsoft.AspNetCore.Mvc;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("alive")]
    public IActionResult Alive() => Ok(new { Status = "Alive" });

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "Healthy" });
}
