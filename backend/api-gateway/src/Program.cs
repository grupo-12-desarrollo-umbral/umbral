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
app.MapReverseProxy();
app.Run();
