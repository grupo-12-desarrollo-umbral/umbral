using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

internal sealed record TestCurrentUser(string? Id, string? Email = null, string? Role = null) : ICurrentUser
{
    public static TestCurrentUser Default { get; } = new("integration-test");
}
