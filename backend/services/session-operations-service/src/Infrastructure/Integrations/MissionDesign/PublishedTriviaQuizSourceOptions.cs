namespace umbral_backend.Infrastructure.Integrations.MissionDesign;

public sealed class PublishedTriviaQuizSourceOptions
{
    public const string SectionName = "PublishedTriviaQuizSource";

    public string BaseAddress { get; set; } = "http://mission-design-service";
}
