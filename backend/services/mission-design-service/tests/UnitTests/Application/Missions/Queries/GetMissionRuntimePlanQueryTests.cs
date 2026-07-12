using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Missions;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;

namespace umbral_backend.Application.UnitTests.Application.Missions.Queries;

// Covers the runtime-plan query handler (found + not-found) and the Id-greater-than-zero validators
// for the runtime-plan and readiness queries. The fully populated DTO also exercises the runtime
// plan record constructors end to end.
public sealed class GetMissionRuntimePlanQueryTests
{
    private static MissionRuntimePlanDto SamplePlan() =>
        new(
            "Mission",
            45,
            new List<MissionRuntimePlanStageDto>
            {
                new(
                    "Stage",
                    1,
                    new List<MissionRuntimePlanSubstageDto>
                    {
                        new(
                            "Hunt",
                            1,
                            "TreasureHunt",
                            new List<MissionRuntimePlanTargetDto>
                            {
                                new("Target", "QR-1", 1, true, 150, 4.711, -74.0721, new MissionRuntimePlanClueDto("Find it", "HiddenUntilOperatorRelease")),
                            },
                            new List<MissionRuntimePlanTriviaQuestionDto>
                            {
                                new(
                                    "Prompt?",
                                    1,
                                    new List<MissionRuntimePlanTriviaOptionDto> { new("A", 1, true), new("B", 2, false) },
                                    10,
                                    30),
                            },
                            new List<MissionRuntimePlanClueDto>
                            {
                                new("Find it", "HiddenUntilOperatorRelease"),
                            }),
                    }),
            });

    [Fact]
    public async Task Handle_WhenPlanExists_ReturnsIt()
    {
        var plan = SamplePlan();
        var repository = new Mock<IMissionReadModelRepository>();
        repository
            .Setup(r => r.GetMissionRuntimePlanAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        var handler = new GetMissionRuntimePlanQueryHandler(repository.Object);

        var result = await handler.Handle(new GetMissionRuntimePlanQuery(7), CancellationToken.None);

        result.Should().BeSameAs(plan);
        result.Stages.Single().Substages.Single().Targets.Single().Clue!.Text.Should().Be("Find it");
        result.Stages.Single().Substages.Single().TriviaQuestions.Single().Options.Should().HaveCount(2);
        result.Stages.Single().Substages.Single().Clues.Single().Text.Should().Be("Find it");
    }

    [Fact]
    public async Task Handle_WhenPlanMissing_ThrowsNotFound()
    {
        var repository = new Mock<IMissionReadModelRepository>();
        repository
            .Setup(r => r.GetMissionRuntimePlanAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionRuntimePlanDto?)null);
        var handler = new GetMissionRuntimePlanQueryHandler(repository.Object);

        var act = async () => await handler.Handle(new GetMissionRuntimePlanQuery(7), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void RuntimePlanValidator_EnforcesPositiveId(int id, bool expectedValid)
    {
        new GetMissionRuntimePlanQueryValidator()
            .Validate(new GetMissionRuntimePlanQuery(id)).IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void ReadinessValidator_EnforcesPositiveId(int id, bool expectedValid)
    {
        new GetMissionReadinessQueryValidator()
            .Validate(new GetMissionReadinessQuery(id)).IsValid.Should().Be(expectedValid);
    }
}
