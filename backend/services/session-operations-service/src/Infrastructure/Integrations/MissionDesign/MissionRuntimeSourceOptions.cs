namespace umbral_backend.Infrastructure.Integrations.MissionDesign;

public sealed class MissionRuntimeSourceOptions
{
    public const string SectionName = "MissionRuntimeSource";

    public string BaseAddress { get; set; } = "http://mission-design-service";
}
