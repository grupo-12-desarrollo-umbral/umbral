namespace umbral_backend.Application.Sessions.DTOs;

/// <summary>
/// Real-time payload pushed to the <c>live-session:{id}</c> group when a transition lands.
/// </summary>
public sealed record SessionStateChangedNotificationDto(
    Guid LiveSessionId,
    string PreviousState,
    string CurrentState,
    DateTimeOffset ChangedAt);
