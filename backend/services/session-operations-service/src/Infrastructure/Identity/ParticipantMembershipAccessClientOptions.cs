using System.Diagnostics.CodeAnalysis;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class ParticipantMembershipAccessClientOptions
{
    public const string SectionName = "IdentityAccess";

    public string BaseAddress { get; set; } = "http://identity-access-service";
}
