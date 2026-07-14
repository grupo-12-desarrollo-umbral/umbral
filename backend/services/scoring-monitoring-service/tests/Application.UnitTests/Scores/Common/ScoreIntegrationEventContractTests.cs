using MassTransit;
using umbral_backend.Application.Scores.Common;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Common;

public sealed class ScoreIntegrationEventContractTests
{
    [Fact]
    public void AnswerRegisteredIntegrationEvent_ShouldDeclareConsumedEntityName()
    {
        typeof(AnswerRegisteredIntegrationEvent)
            .GetCustomAttributes(typeof(EntityNameAttribute), inherit: false)
            .Should()
            .ContainSingle()
            .Which.Should().BeOfType<EntityNameAttribute>()
            .Which.EntityName.Should().Be("session-answer-registered");
    }

    [Fact]
    public void ScoreEntryRegisteredIntegrationEvent_ShouldDeclarePublishedEntityName()
    {
        typeof(ScoreEntryRegisteredIntegrationEvent)
            .GetCustomAttributes(typeof(EntityNameAttribute), inherit: false)
            .Should()
            .ContainSingle()
            .Which.Should().BeOfType<EntityNameAttribute>()
            .Which.EntityName.Should().Be("scoring-score-entry-registered");
    }
}
