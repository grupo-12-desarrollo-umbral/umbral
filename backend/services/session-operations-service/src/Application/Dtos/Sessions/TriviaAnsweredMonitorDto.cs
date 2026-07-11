namespace umbral_backend.Application.Dtos.Sessions;

// Operator-only pre-close monitor payload (HU-36A). Carries the active-question identity
// (SubstageSnapshotId, QuestionSequenceOrder) plus the per-team answered/not-answered roster.
// Structurally omits SelectedOptionSequenceOrder / IsCorrect / ScoreValue — the option a team chose
// can never be revealed before the question closes (HU-35 / HU-36B own reveal + review).
public sealed record TriviaAnsweredMonitorDto(
    Guid LiveSessionId,
    Guid SubstageSnapshotId,
    int QuestionSequenceOrder,
    IReadOnlyList<TriviaTeamAnsweredStatusDto> Teams);

public sealed record TriviaTeamAnsweredStatusDto(
    Guid TeamId,
    string TeamCode,
    string DisplayName,
    bool Answered,
    DateTimeOffset? AnsweredAt);
