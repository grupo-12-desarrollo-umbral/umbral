using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Pushes question lifecycle events (activate/close/substage-advance) to the live-session group
/// over the shared <see cref="SessionsHub"/>.
/// </summary>
public sealed class SignalRSessionQuestionBroadcaster : ISessionQuestionBroadcaster
{
    public const string QuestionActivatedMethod = "QuestionActivated";
    public const string QuestionClosedMethod = "QuestionClosed";
    public const string SubstageAdvancedMethod = "SubstageAdvanced";
    public const string SubstageRankingRevealStartedMethod = "SubstageRankingRevealStarted";

    private readonly IHubContext<SessionsHub> _hubContext;

    public SignalRSessionQuestionBroadcaster(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastQuestionActivatedAsync(
        QuestionActivatedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group($"live-session:{notification.LiveSessionId:D}")
            .SendAsync(QuestionActivatedMethod, notification, cancellationToken);
    }

    public Task BroadcastQuestionClosedAsync(
        QuestionClosedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group($"live-session:{notification.LiveSessionId:D}")
            .SendAsync(QuestionClosedMethod, notification, cancellationToken);
    }

    public Task BroadcastSubstageAdvancedAsync(
        SubstageAdvancedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group($"live-session:{notification.LiveSessionId:D}")
            .SendAsync(SubstageAdvancedMethod, notification, cancellationToken);
    }

    public Task BroadcastSubstageRankingRevealStartedAsync(
        SubstageRankingRevealStartedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group($"live-session:{notification.LiveSessionId:D}")
            .SendAsync(SubstageRankingRevealStartedMethod, notification, cancellationToken);
    }
}
