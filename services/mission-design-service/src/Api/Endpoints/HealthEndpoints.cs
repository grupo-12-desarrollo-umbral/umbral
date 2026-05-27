namespace umbral_backend.Web.Endpoints;

public class HealthEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
        groupBuilder.MapGet("/alive", () => Results.Ok(new { Status = "Alive" }));
    }
}
