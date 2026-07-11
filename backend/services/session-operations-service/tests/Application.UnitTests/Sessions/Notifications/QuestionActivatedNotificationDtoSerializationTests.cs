using System.Text.Json;
using System.Text.Json.Serialization;

namespace umbral_backend.Application.UnitTests.Sessions.Notifications;

public sealed class QuestionActivatedNotificationDtoSerializationTests
{
    [Fact]
    public void Serialize_ContainsTriviaSubstageSnapshotId()
    {
        var substageId = Guid.NewGuid();
        var dto = new QuestionActivatedNotificationDto(
            LiveSessionId: Guid.NewGuid(),
            QuestionIndex: 0,
            SequenceOrder: 1,
            Prompt: "Test prompt",
            Options: new[] { "A", "B" },
            TimeLimitSeconds: 30,
            ActivatedAt: new DateTimeOffset(2026, 7, 11, 10, 0, 0, TimeSpan.Zero),
            TriviaSubstageSnapshotId: substageId);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("triviaSubstageSnapshotId", out var prop).Should().BeTrue();
        prop.GetGuid().Should().Be(substageId);
    }
}
