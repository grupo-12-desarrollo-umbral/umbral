namespace umbral_backend.Infrastructure.Integrations.MissionDesign;

public sealed class MissionReadinessSourceOptions
{
    public const string SectionName = "MissionReadinessSource";

    public string BaseAddress { get; set; } = "http://mission-design-service";
}
