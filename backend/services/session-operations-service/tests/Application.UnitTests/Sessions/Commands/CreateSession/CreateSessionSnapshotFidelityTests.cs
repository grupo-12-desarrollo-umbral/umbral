using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.CreateSession;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.CreateSession;

// HU-16 (DES-75) X.2: locks the trivia-slice fidelity depth of the creation obligation.
// The single CreateSessionCommandHandler must copy the *whole* published quiz into
// MissionRuntimeSnapshot.TriviaQuestionSnapshots — every question and option, with content
// parity (prompt/score/timer/explanation/correct-flag) in strict mission order, no partial copy.
public sealed class CreateSessionSnapshotFidelityTests
{
    private const int MissionId = 7;

    [Fact]
    public async Task CreateAsync_CopiesTheWholePublishedQuiz_WithContentParityInStrictMissionOrder()
    {
        var command = new CreateSessionCommand(
            MissionId,
            "Mission Session",
            15,
            new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero));

        LiveSession? persisted = null;
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveSession, CancellationToken>((session, _) => persisted = session)
            .Returns(Task.CompletedTask);

        // Source questions/options are deliberately supplied out of sequence order so the assertion
        // proves the snapshot re-orders by SequenceOrder rather than echoing input order.
        var sourceQuestions = new[]
        {
            new MissionRuntimeTriviaQuestionDto(
                "Second question?", 2, 40, 60, "Second explanation",
                [
                    new MissionRuntimeTriviaOptionDto("2-B", 2, true),
                    new MissionRuntimeTriviaOptionDto("2-A", 1, false),
                ]),
            new MissionRuntimeTriviaQuestionDto(
                "Third question?", 3, 75, 90, null,
                [
                    new MissionRuntimeTriviaOptionDto("3-A", 1, false),
                    new MissionRuntimeTriviaOptionDto("3-C", 3, true),
                    new MissionRuntimeTriviaOptionDto("3-B", 2, false),
                ]),
            new MissionRuntimeTriviaQuestionDto(
                "First question?", 1, 10, 30, "First explanation",
                [
                    new MissionRuntimeTriviaOptionDto("1-A", 1, true),
                    new MissionRuntimeTriviaOptionDto("1-B", 2, false),
                ]),
        };

        var runtime = new MissionRuntimeDto(
            "Foundations of Science",
            45,
            [
                new MissionRuntimeStageDto(
                    "Stage One",
                    1,
                    [
                        new MissionRuntimeSubstageDto(
                            "Trivia Round", 1, SubstagePlayMode.Trivia.ToString(), [], sourceQuestions)
                    ])
            ]);

        var handler = CreateHandler(repository, runtime);

        await handler.Handle(command, CancellationToken.None);

        persisted.Should().NotBeNull();
        var snapshots = persisted!.MissionRuntimeSnapshot.TriviaQuestionSnapshots;

        // No partial copy: exactly as many question snapshots as the source quiz.
        snapshots.Should().HaveCount(sourceQuestions.Length);

        var expected = sourceQuestions.OrderBy(question => question.SequenceOrder).ToArray();
        var actual = snapshots.OrderBy(snapshot => snapshot.SequenceOrder).ToArray();

        // Snapshots are in strict mission (sequence) order.
        actual.Select(snapshot => snapshot.SequenceOrder).Should().Equal(expected.Select(question => question.SequenceOrder));

        for (var i = 0; i < expected.Length; i++)
        {
            var source = expected[i];
            var snapshot = actual[i];

            snapshot.Prompt.Should().Be(source.Prompt);
            snapshot.SequenceOrder.Should().Be(source.SequenceOrder);
            snapshot.ScoreValue.Should().Be(source.ScoreValue);
            snapshot.TimeLimitSeconds.Should().Be(source.TimeLimitSeconds);
            snapshot.Explanation.Should().Be(source.Explanation);

            var expectedOptions = source.Options.OrderBy(option => option.SequenceOrder).ToArray();
            var actualOptions = snapshot.Options.OrderBy(option => option.SequenceOrder).ToArray();

            actualOptions.Should().HaveCount(expectedOptions.Length);
            for (var j = 0; j < expectedOptions.Length; j++)
            {
                actualOptions[j].OptionText.Should().Be(expectedOptions[j].OptionText);
                actualOptions[j].SequenceOrder.Should().Be(expectedOptions[j].SequenceOrder);
                actualOptions[j].IsCorrect.Should().Be(expectedOptions[j].IsCorrect);
            }

            // Exactly-one-correct flag is preserved per question.
            actualOptions.Count(option => option.IsCorrect).Should().Be(1);
        }
    }

    private static CreateSessionCommandHandler CreateHandler(Mock<ILiveSessionRepository> repository, MissionRuntimeDto runtime)
    {
        var readinessSource = new Mock<IMissionReadinessSource>();
        readinessSource
            .Setup(source => source.GetByIdAsync(MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MissionReadinessDto(MissionId, "Ready", IsActive: true, IsReady: true, []));

        var runtimeSource = new Mock<IMissionRuntimeSource>();
        runtimeSource
            .Setup(source => source.GetByIdAsync(MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runtime);

        return new CreateSessionCommandHandler(
            repository.Object,
            readinessSource.Object,
            runtimeSource.Object,
            new SessionCreationPolicy());
    }
}
