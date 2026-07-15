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
    public void ScoreEntry_ShouldExposeNoPublicMutatorBeyondGrantFactory()
    {
        var publicMethods = typeof(ScoreEntry)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        publicMethods.Should().Equal(nameof(ScoreEntry.Grant));
    }
}
