namespace umbral_backend.Infrastructure.Identity;

public sealed class SessionOperationsClientOptions
{
    public const string SectionName = "SessionOperations";

    public string BaseAddress { get; set; } = "http://session-operations-service:8080";
}
