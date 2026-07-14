using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Commands.RecordScoreEntry;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Commands.RecordScoreEntry;

public sealed class RecordScoreEntryCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEntryIsNew_PersistsSingleGrantUsingInjectedScorePolicy()
    {
        var repository = new Mock<IScoreEntryRepository>();
        repository
            .Setup(repo => repo.ExistsForSourceAsync(ScoreSourceType.TriviaAnswerSubmission, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var awardedScore = ScoreValue.Create(250);
        var scorePolicy = new Mock<IScorePolicy>();
        scorePolicy
            .Setup(policy => policy.Award(It.Is<ScoreValue>(value => value.Value == 100)))
            .Returns(awardedScore);

        ScoreEntry? savedEntry = null;
        repository
            .Setup(repo => repo.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreEntry, CancellationToken>((entry, _) => savedEntry = entry)
            .Returns(Task.CompletedTask);

        var recordedAt = new DateTimeOffset(2026, 7, 14, 18, 0, 0, TimeSpan.Zero);
        var sourceEntityId = Guid.NewGuid();
        var handler = new RecordScoreEntryCommandHandler(repository.Object, scorePolicy.Object);

        await handler.Handle(
            new RecordScoreEntryCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "trivia-answer-correct",
                100,
                recordedAt,
                ScoreSourceType.TriviaAnswerSubmission,
                sourceEntityId),
            CancellationToken.None);

        scorePolicy.Verify(policy => policy.Award(It.Is<ScoreValue>(value => value.Value == 100)), Times.Once);
        repository.Verify(repo => repo.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        savedEntry.Should().NotBeNull();
        savedEntry!.ScoreValue.Should().Be(awardedScore);
        savedEntry.SourceEntityId.Should().Be(sourceEntityId);
        savedEntry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ScoreEntryRegistered>();
    }

    [Fact]
    public async Task Handle_WhenSubmissionWasAlreadyRecorded_DoesNotPersistSecondEntry()
    {
        var repository = new Mock<IScoreEntryRepository>();
        repository
            .Setup(repo => repo.ExistsForSourceAsync(ScoreSourceType.TriviaAnswerSubmission, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var scorePolicy = new Mock<IScorePolicy>();
        var handler = new RecordScoreEntryCommandHandler(repository.Object, scorePolicy.Object);

        await handler.Handle(
            new RecordScoreEntryCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "trivia-answer-correct",
                100,
                DateTimeOffset.UtcNow,
                ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid()),
            CancellationToken.None);

        scorePolicy.Verify(policy => policy.Award(It.IsAny<ScoreValue>()), Times.Never);
        repository.Verify(repo => repo.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
