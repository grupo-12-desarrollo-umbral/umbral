using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Yarp.ReverseProxy.Forwarder;

namespace ApiGateway;

/// <summary>
/// Gives YARP's own proxy failures an RFC 7807 body. When a destination refuses the connection or a
/// request times out, YARP sets a 502/504 status but writes no body, so a client sees a bare status
/// with no <c>traceId</c> — the worst place to lose the trace, since it is the first hop. This
/// middleware fills that body in after the proxy has run.
/// </summary>
/// <remarks>
/// It acts only when the forwarder itself failed (an <see cref="IForwarderErrorFeature"/> is present)
/// and the response has not begun. A downstream that returned its own response — <c>problem+json</c>
/// or otherwise — sets no error feature, so it passes through untouched and is never re-wrapped.
/// </remarks>
public static class ForwarderErrorProblemDetailsMiddleware
{
    public static IApplicationBuilder UseForwarderErrorProblemDetails(this IApplicationBuilder app) =>
        app.Use(static async (context, next) =>
        {
            await next();

            var errorFeature = context.Features.Get<IForwarderErrorFeature>();
            if (errorFeature is null || context.Response.HasStarted)
            {
                return;
            }

            // A timeout is the one forwarder failure with a distinct, well-understood status; every
            // other failure (unreachable destination, DNS miss, reset connection) is a bad gateway.
            var isTimeout = errorFeature.Error is ForwarderError.RequestTimedOut;
            var status = isTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;

            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var problemDetails = new ProblemDetails
            {
                Type = isTimeout ? "gateway-timeout" : "bad-gateway",
                Title = isTimeout ? "Gateway timeout." : "Bad gateway.",
                Detail = isTimeout
                    ? "The upstream service did not respond in time."
                    : "The upstream service is unavailable.",
                Status = status
            };
            problemDetails.Extensions["traceId"] = traceId;

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: "application/problem+json",
                context.RequestAborted);
        });
}
