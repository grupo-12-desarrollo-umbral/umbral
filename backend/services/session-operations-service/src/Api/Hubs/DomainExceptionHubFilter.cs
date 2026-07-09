using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Hub analogue of <see cref="Api.Services.ProblemDetailsExceptionHandler"/>: maps domain and
/// validation failures raised by a hub invocation to a stable, machine-readable code carried in the
/// <see cref="HubException"/> message. SignalR always forwards <see cref="HubException"/> messages to
/// clients (other exceptions are masked unless <c>EnableDetailedErrors</c> is on), so the code reaches
/// the client identically in every environment. Clients key on the <c>code</c>, never the English prose.
/// </summary>
/// <remarks>
/// The code derives from the same <see cref="IErrorMetadata"/> / <see cref="ErrorCategory"/> vocabulary
/// the REST <see cref="Api.Services.ProblemDetailsExceptionHandler"/> uses, so the two share one source of
/// truth and a new exception is classified automatically instead of silently collapsing to <c>ERROR</c>.
/// A handful of curated codes are kept as explicit overrides because they are finer-grained than their
/// <see cref="ErrorCategory"/> — the mobile reconnect policy branches on the specific situation, so those
/// strings are part of the hub's published contract and must stay stable.
/// </remarks>
/// <remarks>
/// The <c>ERROR</c> arm catches what no one anticipated, so its message may carry constraint or
/// column names straight to a mobile client. That arm alone substitutes a generic message and
/// correlates to the logged exception through a <c>traceId</c>; the curated codes are unchanged.
/// </remarks>
public sealed class DomainExceptionHubFilter(ILogger<DomainExceptionHubFilter> logger) : IHubFilter
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Keeps the five curated payloads byte-identical to before traceId existed.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception exception) when (exception is not HubException and not OperationCanceledException)
        {
            throw new HubException(BuildPayload(exception, invocationContext));
        }
    }

    private string BuildPayload(Exception exception, HubInvocationContext invocationContext)
    {
        var code = MapCode(exception);
        if (code != "ERROR")
        {
            return JsonSerializer.Serialize(
                new HubErrorPayload(code, exception.Message),
                PayloadOptions);
        }

        // No HttpContext inside a hub invocation, so the connection id is the fallback correlator.
        var traceId = Activity.Current?.Id ?? invocationContext.Context.ConnectionId;
        logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);

        return JsonSerializer.Serialize(
            new HubErrorPayload(code, "An unexpected error occurred.", traceId),
            PayloadOptions);
    }

    private static string MapCode(Exception exception) => exception switch
    {
        // Curated codes finer than their ErrorCategory: the mobile reconnect policy branches on the
        // exact situation, so these strings are contractual and must precede the category fallback.
        LateJoinNotAllowedException => "LATE_JOIN_NOT_ALLOWED",
        ParticipantRemovedFromSessionException => "PARTICIPANT_REMOVED",
        ParticipantAssignedToDifferentTeamException => "WRONG_TEAM",
        ParticipantAlreadyConnectedException => "ALREADY_CONNECTED",
        TeamCapacityReachedException or TeamJoinClosedException => "TEAM_UNAVAILABLE",
        // Everything else carrying error metadata derives its code from the shared ErrorCategory,
        // so a new exception is classified automatically instead of silently collapsing to "ERROR".
        IErrorMetadata metadata => CodeFor(metadata.Category),
        UnauthorizedAccessException => "UNAUTHORIZED",
        _ => "ERROR"
    };

    private static string CodeFor(ErrorCategory category) => category switch
    {
        ErrorCategory.NotFound => "NOT_FOUND",
        ErrorCategory.Validation => "VALIDATION_FAILED",
        ErrorCategory.Conflict => "CONFLICT",
        ErrorCategory.Forbidden => "FORBIDDEN",
        ErrorCategory.Unauthorized => "UNAUTHORIZED",
        ErrorCategory.Unprocessable => "UNPROCESSABLE",
        _ => "ERROR"
    };

    private sealed record HubErrorPayload(string Code, string Message, string? TraceId = null);
}
