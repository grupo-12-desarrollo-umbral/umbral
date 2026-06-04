using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services.SessionStates;

internal static class LiveSessionStateFactory
{
    private static readonly ILiveSessionState Scheduled = new ScheduledLiveSessionState();
    private static readonly ILiveSessionState Preparing = new PreparingLiveSessionState();
    private static readonly ILiveSessionState Active = new ActiveLiveSessionState();
    private static readonly ILiveSessionState Paused = new PausedLiveSessionState();
    private static readonly ILiveSessionState Finished = new FinishedLiveSessionState();
    private static readonly ILiveSessionState Cancelled = new CancelledLiveSessionState();

    internal static ILiveSessionState For(SessionState state)
    {
        return state switch
        {
            SessionState.Scheduled => Scheduled,
            SessionState.Preparing => Preparing,
            SessionState.Active => Active,
            SessionState.Paused => Paused,
            SessionState.Finished => Finished,
            SessionState.Cancelled => Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown live session state.")
        };
    }
}
