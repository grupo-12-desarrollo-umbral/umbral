using MassTransit;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.Application.UnitTests.Scores.Common;

// Locks the service-local contract's exchange binding and exercises the record value semantics
// (equality, hashing, deconstruction) that the compiler synthesises for both the integration
// event and the internal command.
public sealed class AnswerRegisteredIntegrationEventTests
{
    private static AnswerRegisteredIntegrationEvent Sample(Guid sessionId) => new(
        LiveSessionId: sessionId,
        TeamId: Guid.NewGuid(),
        TriviaAnswerSubmissionId: Guid.NewGuid(),
        TriviaSubstageSnapshotId: Guid.NewGuid(),
        QuestionSequenceOrder: 3,
        SelectedOptionSequenceOrder: 2,
        IsCorrect: true,
        ScoreValue: 20,
        SubmittedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public void Contract_BindsToSessionAnswerRegisteredExchange()
    {
        var entityName = typeof(AnswerRegisteredIntegrationEvent)
            .GetCustomAttributes(typeof(EntityNameAttribute), inherit: false)
            .Cast<EntityNameAttribute>()
            .Single();

        entityName.EntityName.Should().Be("session-answer-registered");
    }

    [Fact]
    public void IntegrationEvent_ValueEquality_HoldsAndDiffers()
    {
        var sessionId = Guid.NewGuid();
        var first = Sample(sessionId);
        var second = first with { };
        var different = first with { LiveSessionId = Guid.NewGuid() };

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.Should().NotBe(different);
        first.ToString().Should().Contain(nameof(AnswerRegisteredIntegrationEvent.LiveSessionId));
    }

    [Fact]
    public void Command_ValueEquality_HoldsAndDeconstructs()
    {
        var teamId = Guid.NewGuid();
        var command = new RecordAnswerReceiptCommand(
            Guid.NewGuid(), teamId, Guid.NewGuid(), Guid.NewGuid(), 1, 0, false, 0, DateTimeOffset.UnixEpoch);
        var copy = command with { };

        command.Should().Be(copy);
        command.GetHashCode().Should().Be(copy.GetHashCode());
        command.Should().NotBe(command with { TeamId = Guid.NewGuid() });

        var (_, deconstructedTeamId, _, _, _, _, _, _, _) = command;
        deconstructedTeamId.Should().Be(teamId);
        command.ToString().Should().Contain(nameof(RecordAnswerReceiptCommand.TeamId));
    }
}
