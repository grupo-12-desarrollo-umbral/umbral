using System.Diagnostics.CodeAnalysis;

namespace umbral_backend.Infrastructure.Identity;

[ExcludeFromCodeCoverage]
public sealed class ParticipantMembershipAccessClientOptions
{
    public const string SectionName = "UsersService";

    public string BaseAddress { get; set; } = "http://users-service";
}
