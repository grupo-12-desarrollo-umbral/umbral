using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Api.Controllers;

[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> CheckHealthAsync(
        [FromServices] IDatabaseHealthCheck healthCheck,
        CancellationToken cancellationToken)
    {
        var canConnect = await healthCheck.CanConnectAsync(cancellationToken);

        return canConnect
            ? Ok(new { Status = "Healthy" })
            : Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unavailable");
    }

    [HttpGet("alive")]
    public IActionResult Alive() => Ok(new { Status = "Alive" });
}
