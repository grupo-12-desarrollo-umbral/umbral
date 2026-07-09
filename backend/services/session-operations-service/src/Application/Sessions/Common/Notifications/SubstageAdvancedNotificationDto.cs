namespace umbral_backend.Application.Sessions.Common.Notifications;

// Broadcast when the timer-driven round crosses a substage boundary (ADR-0005). ToSubstageId is
// null when the final substage completed and the session finished.
public sealed record SubstageAdvancedNotificationDto(
    Guid LiveSessionId,
    Guid FromSubstageId,
    string FromPlayMode,
    Guid? ToSubstageId,
    DateTimeOffset AdvancedAt);
