using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Web.Endpoints;

public class HealthEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/health", CheckHealthAsync);
        groupBuilder.MapGet("/alive", () => Results.Ok(new { Status = "Alive" }));
    }

    private static async Task<IResult> CheckHealthAsync(
        IDatabaseHealthCheck healthCheck,
        CancellationToken cancellationToken)
    {
        var canConnect = await healthCheck.CanConnectAsync(cancellationToken);

        return canConnect
            ? Results.Ok(new { Status = "Healthy" })
            : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unavailable");
    }
}
