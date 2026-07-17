var builder = WebApplication.CreateBuilder(args);
builder.AddGatewayServices();

var app = builder.Build();
// First in the pipeline so it catches anything the middleware below throw; the handler owns the
// generic problem+json 500, and no exception message reaches the client.
app.UseExceptionHandler(static _ => { });
// Turns YARP's bodiless 502/504 proxy failures into problem+json without touching downstream responses.
app.UseForwarderErrorProblemDetails();
app.UseCors(DependencyInjection.FrontendCorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
// Per-route rate limiting (the register route opts in via its RateLimiterPolicy metadata); must sit
// before MapReverseProxy so YARP applies the named policy to the matched route.
app.UseRateLimiter();
// Liveness only: "is this process accepting requests", not readiness. The gateway owns no database
// and deliberately does NOT aggregate downstream health — coupling its liveness to the services would
// let one degraded backend pull the whole gateway out of an LB's rotation while the other routes are
// fine. Downstream health is each service's own /health. AllowAnonymous so the auth middleware above
// never challenges an orchestrator's probe; a literal segment also outranks YARP's catch-all routes.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapReverseProxy();
app.Run();

// Exposes the implicit top-level Program to WebApplicationFactory in ApiGateway.IntegrationTests.
public partial class Program;
