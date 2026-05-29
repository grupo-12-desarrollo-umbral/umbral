using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Web.Endpoints;

public class HealthEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/health", CheckHealthAsync);
        groupBuilder.MapGet("/alive", () => Results.Ok(new { Status = "Alive" }));
    }

    private static async Task<IResult> CheckHealthAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? Results.Ok(new { Status = "Healthy" })
            : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database unavailable");
    }
}
