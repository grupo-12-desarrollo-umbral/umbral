namespace umbral_backend.Application.Sessions.Common.Notifications;

// Operator-only "team answered" indicator (HU-34). It intentionally carries NO IsCorrect, NO
// ScoreValue, and NO selected option — only that a team registered an answer to a given question.
// Correctness/points are withheld from every real-time surface (fairness): they surface after close
// (HU-35) or via downstream scoring (HU-37). Broadcast to the operator-only group only, never to the
// participant-visible live-session group.
public sealed record TeamAnsweredNotificationDto(
    Guid LiveSessionId,
    Guid TeamId,
    Guid TriviaSubstageSnapshotId,
    int QuestionSequenceOrder,
    DateTimeOffset AnsweredAt);
