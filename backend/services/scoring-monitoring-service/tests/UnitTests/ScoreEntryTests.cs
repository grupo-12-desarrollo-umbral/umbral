using System.Reflection;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
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
    public void Penalty_ShouldCreateAppendOnlyPenaltyLedgerFact_AndRaiseBothDomainEvents()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var appliedByUserId = Guid.NewGuid();
        var deductionValue = ScoreValue.Create(50);

        var entry = ScoreEntry.Penalty(
            liveSessionId,
            teamId,
            "Unsportsmanlike conduct",
            deductionValue,
            appliedByUserId);

        entry.EntryType.Should().Be(ScoreEntryType.Penalty);
        entry.ScoreValue.Should().Be(deductionValue);
        entry.SourceEntityType.Should().Be(ScoreSourceType.Penalty);
        entry.SourceEntityId.Should().NotBe(Guid.Empty);
        entry.ReasonCode.Should().Be("Unsportsmanlike conduct");
        entry.RecordedByUserId.Should().BeNull();

        entry.DomainEvents.Should().HaveCount(2);

        var registeredEvent = entry.DomainEvents
            .OfType<ScoreEntryRegistered>()
            .Should().ContainSingle().Subject;
        registeredEvent.EntryType.Should().Be(ScoreEntryType.Penalty);
        registeredEvent.LiveSessionId.Should().Be(liveSessionId);
        registeredEvent.TeamId.Should().Be(teamId);
        registeredEvent.ScoreValue.Should().Be(50);
        registeredEvent.SourceEntityType.Should().Be(ScoreSourceType.Penalty);

        var penaltyAppliedEvent = entry.DomainEvents
            .OfType<PenaltyApplied>()
            .Should().ContainSingle().Subject;
        penaltyAppliedEvent.LiveSessionId.Should().Be(liveSessionId);
        penaltyAppliedEvent.TeamId.Should().Be(teamId);
        penaltyAppliedEvent.DeductionMagnitude.Should().Be(50);
        penaltyAppliedEvent.AppliedByUserId.Should().Be(appliedByUserId);
        penaltyAppliedEvent.Reason.Should().Be("Unsportsmanlike conduct");
        penaltyAppliedEvent.ScoreEntryId.Should().Be(entry.ScoreEntryId);
        penaltyAppliedEvent.PenaltyId.Should().Be(entry.SourceEntityId);
    }

    [Fact]
    public void Penalty_ShouldRejectNonNegativeDeductionValue()
    {
        var deductionValue = ScoreValue.Create(100);

        var entry = ScoreEntry.Penalty(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid reason",
            deductionValue,
            Guid.NewGuid());

        entry.ScoreValue.Value.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Penalty_ShouldRequireNonBlankReason()
    {
        var deductionValue = ScoreValue.Create(25);

        var act = () => ScoreEntry.Penalty(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "   ",
            deductionValue,
            Guid.NewGuid());

        act.Should().Throw<PenaltyRequiresReasonException>();
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
