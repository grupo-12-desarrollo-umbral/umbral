using System.Globalization;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

public sealed class SessionEvent
{
    public const string StateChangedEventType = "SessionStateChanged";
    public const string QuestionClosedEventType = "QuestionClosed";
    public const string ResultsFinalizedEventType = "SessionResultsFinalized";
    public const string ClueReleasedEventType = "ClueReleased";
    public const string EvidenceSubmittedEventType = "EvidenceSubmitted";
    public const string PenaltyAppliedEventType = "PenaltyApplied";
    public const string ScoreChangedEventType = "ScoreChanged";
    public const string RankingRefreshedEventType = "RankingRefreshed";

    private SessionEvent()
    {
        SourceEventKey = string.Empty;
        EventType = string.Empty;
        PayloadSummary = string.Empty;
    }

    private SessionEvent(
        string sourceEventKey,
        Guid liveSessionId,
        Guid? teamId,
        string eventType,
        DateTimeOffset occurredAt,
        string payloadSummary,
        Guid? responsibleUserExternalId)
    {
        SessionEventId = Guid.NewGuid();
        SourceEventKey = sourceEventKey;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EventType = eventType;
        OccurredAt = occurredAt;
        PayloadSummary = payloadSummary;
        ResponsibleUserExternalId = responsibleUserExternalId;
    }

    public Guid SessionEventId { get; private set; }

    public string SourceEventKey { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid? TeamId { get; private set; }

    public string EventType { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string PayloadSummary { get; private set; }

    public Guid? ResponsibleUserExternalId { get; private set; }

    public static SessionEvent ForStateChange(
        Guid liveSessionId,
        SessionState previousState,
        SessionState currentState,
        DateTimeOffset changedAt,
        Guid? responsibleUserExternalId,
        string? reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var transition = $"{previousState}→{currentState}";
        var summary = normalizedReason is null ? transition : $"{transition}: {normalizedReason}";

        return new SessionEvent(
            BuildSourceEventKey(StateChangedEventType, liveSessionId, changedAt, $"{previousState}:{currentState}"),
            liveSessionId,
            null,
            StateChangedEventType,
            changedAt,
            summary,
            responsibleUserExternalId);
    }

    public static SessionEvent ForQuestionClosed(Guid liveSessionId, int questionIndex, DateTimeOffset closedAt)
    {
        if (questionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionIndex), "Question index cannot be negative.");
        }

        return new SessionEvent(
            BuildSourceEventKey(QuestionClosedEventType, liveSessionId, closedAt, questionIndex.ToString(CultureInfo.InvariantCulture)),
            liveSessionId,
            null,
            QuestionClosedEventType,
            closedAt,
            $"Question {questionIndex} closed",
            null);
    }

    public static SessionEvent ForResultsFinalized(Guid liveSessionId, DateTimeOffset finishedAt)
    {
        return new SessionEvent(
            BuildSourceEventKey(ResultsFinalizedEventType, liveSessionId, finishedAt, null),
            liveSessionId,
            null,
            ResultsFinalizedEventType,
            finishedAt,
            "Session results finalized",
            null);
    }

    public static SessionEvent ForClueReleased(
        Guid liveSessionId,
        Guid teamId,
        Guid? targetId,
        Guid? clueId,
        string releaseMode,
        DateTimeOffset releasedAt,
        Guid? responsibleUserExternalId)
    {
        var discriminator = clueId ?? targetId
            ?? throw new ArgumentException("A clue release requires a clue or target identifier.");

        return new SessionEvent(
            BuildSourceEventKey(
                ClueReleasedEventType,
                liveSessionId,
                releasedAt,
                $"{teamId:D}:{discriminator:D}"),
            liveSessionId,
            teamId,
            ClueReleasedEventType,
            releasedAt,
            $"Clue {discriminator:D} released ({releaseMode})",
            responsibleUserExternalId);
    }

    public static SessionEvent ForEvidenceSubmitted(
        Guid liveSessionId,
        Guid teamId,
        Guid evidenceSubmissionId,
        string evidenceType,
        DateTimeOffset submittedAt)
    {
        return new SessionEvent(
            BuildSourceEventKey(EvidenceSubmittedEventType, liveSessionId, submittedAt, evidenceSubmissionId.ToString("D")),
            liveSessionId,
            teamId,
            EvidenceSubmittedEventType,
            submittedAt,
            $"{evidenceType} evidence {evidenceSubmissionId:D} submitted",
            null);
    }

    public static SessionEvent ForPenaltyApplied(
        Guid liveSessionId,
        Guid teamId,
        Guid penaltyId,
        int scoreValue,
        string reason,
        DateTimeOffset appliedAt,
        Guid? responsibleUserExternalId)
    {
        return new SessionEvent(
            BuildSourceEventKey(PenaltyAppliedEventType, liveSessionId, appliedAt, penaltyId.ToString("D")),
            liveSessionId,
            teamId,
            PenaltyAppliedEventType,
            appliedAt,
            $"Penalty {scoreValue.ToString(CultureInfo.InvariantCulture)}: {reason}",
            responsibleUserExternalId);
    }

    public static SessionEvent ForScoreChanged(
        Guid liveSessionId,
        Guid teamId,
        Guid scoreEntryId,
        int scoreValue,
        string reasonCode,
        DateTimeOffset recordedAt)
    {
        return new SessionEvent(
            BuildSourceEventKey(ScoreChangedEventType, liveSessionId, recordedAt, scoreEntryId.ToString("D")),
            liveSessionId,
            teamId,
            ScoreChangedEventType,
            recordedAt,
            $"Score changed by {scoreValue.ToString(CultureInfo.InvariantCulture)} ({reasonCode})",
            null);
    }

    public static SessionEvent ForRankingRefreshed(
        Guid liveSessionId,
        long calculationVersion,
        DateTimeOffset refreshedAt)
    {
        return new SessionEvent(
            BuildSourceEventKey(
                RankingRefreshedEventType,
                liveSessionId,
                refreshedAt,
                calculationVersion.ToString(CultureInfo.InvariantCulture)),
            liveSessionId,
            null,
            RankingRefreshedEventType,
            refreshedAt,
            $"Ranking refreshed to version {calculationVersion.ToString(CultureInfo.InvariantCulture)}",
            null);
    }

    private static string BuildSourceEventKey(
        string eventType,
        Guid liveSessionId,
        DateTimeOffset occurredAt,
        string? discriminator)
    {
        var prefix = $"{eventType}:{liveSessionId:D}:{occurredAt:O}";
        return discriminator is null ? prefix : $"{prefix}:{discriminator}";
    }
}
