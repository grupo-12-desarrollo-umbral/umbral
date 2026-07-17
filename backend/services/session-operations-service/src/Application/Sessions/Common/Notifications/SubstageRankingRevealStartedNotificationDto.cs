namespace umbral_backend.Application.Sessions.Common.Notifications;

// Broadcast when a substage ends and its ranking goes on screen for 10s (D-3), in either play mode.
// Deliberately carries no ranking: that projection belongs to scoring-monitoring, and clients already
// hold it live (GET /api/sessions/{id}/ranking + the scoring hub's RankingChanged). This is the cue to
// present the data they have, full-screen, until RevealUntil.
//
// IsTerminal marks the last substage — the mission finishes on this reveal instead of advancing off
// it, so clients can show the mission-finished state on the same ranking rather than flashing a frame.
public sealed record SubstageRankingRevealStartedNotificationDto(
    Guid LiveSessionId,
    Guid SubstageSnapshotId,
    string PlayMode,
    DateTimeOffset RevealUntil,
    bool IsTerminal,
    DateTimeOffset EmittedAt);
