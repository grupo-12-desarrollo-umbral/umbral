using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.CreateSession;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.CreateSession;

// HU-17 X.2: locks the single-source invariant at the application layer. The handler is the
// only creation entry point; it builds the MissionRuntimeSnapshot solely from the one requested
// mission, and the command carries no non-mission source field.
public sealed class CreateSessionSingleSourceInvariantTests
{
    private const int MissionId = 7;

    [Fact]
    public async Task Handle_BuildsSnapshotSolelyFromTheRequestedMission()
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

        var readinessSource = new Mock<IMissionReadinessSource>();
        readinessSource
            .Setup(source => source.GetByIdAsync(MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MissionReadinessDto(MissionId, "Ready", IsActive: true, IsReady: true, []));

        var runtimeSource = new Mock<IMissionRuntimeSource>();
        runtimeSource
            .Setup(source => source.GetByIdAsync(MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SingleMissionRuntime());

        var handler = new CreateSessionCommandHandler(
            repository.Object,
            readinessSource.Object,
            runtimeSource.Object,
            new SessionCreationPolicy());

        await handler.Handle(command, CancellationToken.None);

        // Only the requested mission was consulted — no second/external source merged in.
        runtimeSource.Verify(source => source.GetByIdAsync(MissionId, It.IsAny<CancellationToken>()), Times.Once);
        runtimeSource.Verify(source => source.GetByIdAsync(It.Is<int>(id => id != MissionId), It.IsAny<CancellationToken>()), Times.Never);
        readinessSource.Verify(source => source.GetByIdAsync(MissionId, It.IsAny<CancellationToken>()), Times.Once);

        persisted.Should().NotBeNull();
        persisted!.Source.SourceType.Should().Be(SessionSourceType.Mission);

        // The session source identity is derived deterministically from command.MissionId alone.
        var expectedSourceId = new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"mission-runtime:{MissionId}")));
        persisted.Source.SourceEntityId.Should().Be(expectedSourceId);

        // The snapshot is bound to the same single mission identity — one session, one source.
        persisted.MissionRuntimeSnapshot.SourceMissionId.Should().Be(expectedSourceId);
        persisted.MissionRuntimeSnapshot.SourceMissionId.Should().Be(persisted.Source.SourceEntityId);
    }

    [Fact]
    public void CreateSessionCommand_CarriesNoNonMissionSourceField()
    {
        var sourceFieldNames = typeof(CreateSessionCommand)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        // MissionId is the only source identity on the command; no quiz/second-source field exists.
        sourceFieldNames.Should().Contain(nameof(CreateSessionCommand.MissionId));
        sourceFieldNames.Should().NotContain(name =>
            name.Contains("Quiz", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Trivia", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplicationLayer_ExposesNoTriviaQuizCreationPath()
    {
        var applicationTypes = typeof(CreateSessionCommand).Assembly.GetTypes();

        // Single creation entry point: no retired CreateTriviaSession* command/handler and no
        // IPublishedTriviaQuizSource read port remain in the Application layer.
        applicationTypes.Should().NotContain(type => type.Name.StartsWith("CreateTriviaSession", StringComparison.Ordinal));
        applicationTypes.Should().NotContain(type => type.Name == "IPublishedTriviaQuizSource");
        applicationTypes.Should().NotContain(type => type.Name == "PublishedTriviaQuizDto");

        // Exactly one type in the layer produces a CreateSessionResultDto, and it is the handler.
        // Comparing against typeof(handler) directly would be a tautology — it must be a search
        // over what the layer actually exposes, so a second creation path trips it.
        applicationTypes
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(MediatR.IRequestHandler<,>)
                && contract.GetGenericArguments()[1] == typeof(CreateSessionResultDto)))
            .Should().ContainSingle()
            .Which.Should().Be(typeof(CreateSessionCommandHandler));

        // Option C: the single-consumer facade is realized by the handler, never a standalone type.
        applicationTypes.Should().NotContain(type =>
            type.Namespace == typeof(CreateSessionCommand).Namespace
            && type.Name.EndsWith("Facade", StringComparison.Ordinal));
    }

    private static MissionRuntimeDto SingleMissionRuntime()
    {
        return new MissionRuntimeDto(
            "Foundations of Science",
            45,
            [
                new MissionRuntimeStageDto(
                    "Stage One",
                    1,
                    [
                        new MissionRuntimeSubstageDto(
                            "Trivia Round",
                            1,
                            SubstagePlayMode.Trivia.ToString(),
                            null,
                            [],
                            [
                                new MissionRuntimeTriviaQuestionDto(
                                    "What is the closest planet to the Sun?",
                                    1,
                                    100,
                                    30,
                                    null,
                                    [
                                        new MissionRuntimeTriviaOptionDto("Mercury", 1, true),
                                        new MissionRuntimeTriviaOptionDto("Venus", 2, false)
                                    ])
                            ])
                    ])
            ]);
    }
}
