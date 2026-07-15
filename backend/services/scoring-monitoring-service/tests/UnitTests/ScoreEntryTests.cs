using System.Reflection;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ScoreEntryTests
{
    [Fact]
    public void Grant_ShouldCreateAppendOnlyLedgerFact_AndRaiseDomainEvent()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var sourceEntityId = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var awardedScore = ScoreValue.Create(150);

        var entry = ScoreEntry.Grant(
            liveSessionId,
            teamId,
            "Gilded Owls",
            "trivia-correct-answer",
            awardedScore,
            recordedAt,
            ScoreSourceType.TriviaAnswerSubmission,
            sourceEntityId,
            recordedByUserId: 88);

        entry.EntryType.Should().Be(ScoreEntryType.Grant);
        entry.TeamDisplayName.Should().Be("Gilded Owls");
        entry.ScoreValue.Should().Be(awardedScore);
        entry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ScoreEntryRegistered>();

        var @event = entry.DomainEvents.Single().Should().BeOfType<ScoreEntryRegistered>().Subject;
        @event.LiveSessionId.Should().Be(liveSessionId);
        @event.TeamId.Should().Be(teamId);
        @event.ScoreValue.Should().Be(150);
        @event.SourceEntityType.Should().Be(ScoreSourceType.TriviaAnswerSubmission);
        @event.SourceEntityId.Should().Be(sourceEntityId);
    }

    [Fact]
    public void Penalty_ShouldCreateAppendOnlyPenaltyLedgerFact_AndRaiseDomainEvent()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var appliedByUserId = Guid.NewGuid();
        var deductionValue = ScoreValue.Create(50);
        var scoreEntryId = Guid.NewGuid();
        var penaltyId = Guid.NewGuid();
        var appliedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

        var entry = ScoreEntry.Penalty(
            scoreEntryId,
            liveSessionId,
            teamId,
            "Crimson Foxes",
            "Unsportsmanlike conduct",
            deductionValue,
            penaltyId,
            appliedByUserId,
            appliedAt);

        entry.EntryType.Should().Be(ScoreEntryType.Penalty);
        entry.TeamDisplayName.Should().Be("Crimson Foxes");
        entry.ScoreValue.Should().Be(deductionValue);
        entry.SourceEntityType.Should().Be(ScoreSourceType.Penalty);
        entry.SourceEntityId.Should().Be(penaltyId);
        entry.ReasonCode.Should().Be("Unsportsmanlike conduct");
        entry.RecordedByUserId.Should().BeNull();
        entry.RecordedAt.Should().Be(appliedAt);

        var registeredEvent = entry.DomainEvents
            .OfType<ScoreEntryRegistered>()
            .Should().ContainSingle().Subject;
        registeredEvent.EntryType.Should().Be(ScoreEntryType.Penalty);
        registeredEvent.LiveSessionId.Should().Be(liveSessionId);
        registeredEvent.TeamId.Should().Be(teamId);
        registeredEvent.ScoreValue.Should().Be(50);
        registeredEvent.SourceEntityType.Should().Be(ScoreSourceType.Penalty);
    }

    [Fact]
    public void Penalty_ShouldRaiseBothScoreEntryRegisteredAndPenaltyApplied()
    {
        // HU-38 AC: the apply flow raises the ledger fact AND the penalty audit event.
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var appliedByUserId = Guid.NewGuid();
        var scoreEntryId = Guid.NewGuid();
        var penaltyId = Guid.NewGuid();
        var appliedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

        var entry = ScoreEntry.Penalty(
            scoreEntryId,
            liveSessionId,
            teamId,
            "Crimson Foxes",
            "Unsportsmanlike conduct",
            ScoreValue.Create(50),
            penaltyId,
            appliedByUserId,
            appliedAt);

        entry.DomainEvents.Should().HaveCount(2);
        entry.DomainEvents.OfType<ScoreEntryRegistered>().Should().ContainSingle();

        var penaltyEvent = entry.DomainEvents
            .OfType<PenaltyApplied>()
            .Should().ContainSingle().Subject;
        penaltyEvent.PenaltyId.Should().Be(penaltyId);
        penaltyEvent.ScoreEntryId.Should().Be(scoreEntryId);
        penaltyEvent.LiveSessionId.Should().Be(liveSessionId);
        penaltyEvent.TeamId.Should().Be(teamId);
        penaltyEvent.DeductionMagnitude.Should().Be(50);
        penaltyEvent.AppliedByUserId.Should().Be(appliedByUserId);
        penaltyEvent.Reason.Should().Be("Unsportsmanlike conduct");

        // The event carries the caller's applied-at verbatim — no second clock read in the factory.
        penaltyEvent.AppliedAt.Should().Be(appliedAt);
    }

    [Fact]
    public void Penalty_ShouldRejectNonNegativeDeductionValue()
    {
        var deductionValue = ScoreValue.Create(100);

        var entry = ScoreEntry.Penalty(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            string.Empty,
            "Valid reason",
            deductionValue,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        entry.ScoreValue.Value.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Penalty_ShouldRequireNonBlankReason()
    {
        var deductionValue = ScoreValue.Create(25);

        var act = () => ScoreEntry.Penalty(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            string.Empty,
            "   ",
            deductionValue,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ScoreEntry_ShouldExposeNoPublicMutatorBeyondGrantAndPenaltyFactories()
    {
        var publicMethods = typeof(ScoreEntry)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order()
            .ToArray();

        publicMethods.Should().Equal(nameof(ScoreEntry.Grant), nameof(ScoreEntry.Penalty));
    }
}
