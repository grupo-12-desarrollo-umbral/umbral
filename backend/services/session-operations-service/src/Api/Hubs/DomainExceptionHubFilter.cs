using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Domain.Exceptions;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Hub analogue of <see cref="Api.Services.ProblemDetailsExceptionHandler"/>: maps domain and
/// validation failures raised by a hub invocation to a stable, machine-readable code carried in the
/// <see cref="HubException"/> message. SignalR always forwards <see cref="HubException"/> messages to
/// clients (other exceptions are masked unless <c>EnableDetailedErrors</c> is on), so the code reaches
/// the client identically in every environment. Clients key on the <c>code</c>, never the English prose.
/// </summary>
public sealed class DomainExceptionHubFilter : IHubFilter
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
            throw new HubException(BuildPayload(exception));
        }
    }

    private static string BuildPayload(Exception exception) =>
        JsonSerializer.Serialize(
            new HubErrorPayload(MapCode(exception), exception.Message),
            PayloadOptions);

    private static string MapCode(Exception exception) => exception switch
    {
        LateJoinNotAllowedException => "LATE_JOIN_NOT_ALLOWED",
        ForbiddenAccessException => "FORBIDDEN",
        ParticipantRemovedFromSessionException => "PARTICIPANT_REMOVED",
        ParticipantAssignedToDifferentTeamException => "WRONG_TEAM",
        ParticipantAlreadyConnectedException => "ALREADY_CONNECTED",
        TeamCapacityReachedException or TeamJoinClosedException => "TEAM_UNAVAILABLE",
        ValidationException => "VALIDATION_FAILED",
        NotFoundException or TeamNotFoundException => "NOT_FOUND",
        UnauthorizedAccessException => "UNAUTHORIZED",
        _ => "ERROR"
    };

    private sealed record HubErrorPayload(string Code, string Message);
}
