using System.Net;
using System.Text;
using System.Text.Json;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Infrastructure.Identity;

namespace umbral_backend.Infrastructure.IntegrationTests.Identity;

public sealed class AuthenticatedActorProfileAccessClientTests
{
    [Fact]
    public async Task GetCurrentAsync_ReturnsCurrentActorAndForwardsTrustedHeaders()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(new
        {
            userId = 27,
            externalIdentityId = "kc-operator-27",
            displayName = "Operator 27",
            email = "operator27@example.com",
            role = "Operator",
            isActive = true
        }));

        var client = CreateClient(handler);

        var result = await client.GetCurrentAsync(CancellationToken.None);

        result.Should().Be(new AuthenticatedActorProfileLookupDto(27, "kc-operator-27", "Operator", true));

        handler.Requests.Should().ContainSingle();
        var request = handler.Requests.Single();
        request.RequestUri!.PathAndQuery.Should().Be("/api/users/me");
        request.Headers.GetValues("X-User-Id").Should().ContainSingle().Which.Should().Be("kc-admin-01");
        request.Headers.GetValues("X-User-Role").Should().ContainSingle().Which.Should().Be("Administrator");
        request.Headers.GetValues("X-User-Email").Should().ContainSingle().Which.Should().Be("admin@example.com");
    }

    private static AuthenticatedActorProfileAccessClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://users-service")
        };

        return new AuthenticatedActorProfileAccessClient(
            httpClient,
            new StubCurrentUser("kc-admin-01", "admin@example.com", "Administrator"));
    }

    private static HttpResponseMessage CreateJsonResponse(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed record StubCurrentUser(
        string? Id,
        string? Email,
        string? Role,
        string DisplayName = "Stub User") : umbral_backend.Application.Common.Interfaces.ICurrentUser;

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responseFactory(request));
        }
    }
}
