using System.Threading;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/probe/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/probe/reset", () =>
{
    ProbeState.Reset();
    return Results.NoContent();
});

app.MapGet("/probe/state", () => Results.Ok(new ProbeStateResponse(ProbeState.HitCount)));

app.MapGet("/api/test-auth-probe/inspect", (HttpRequest request) =>
{
    ProbeState.RecordHit();

    return Results.Ok(new ProbeInspectResponse(
        request.Headers["X-User-Id"].ToString(),
        request.Headers["X-User-Role"].ToString(),
        request.Headers["X-User-Email"].ToString(),
        request.Headers["Authorization"].ToString()));
});

app.Run();

internal static class ProbeState
{
    private static int _hitCount;

    public static int HitCount => Volatile.Read(ref _hitCount);

    public static void RecordHit() => Interlocked.Increment(ref _hitCount);

    public static void Reset() => Interlocked.Exchange(ref _hitCount, 0);
}

internal sealed record ProbeStateResponse(int HitCount);

internal sealed record ProbeInspectResponse(
    string? XUserId,
    string? XUserRole,
    string? XUserEmail,
    string? Authorization);
