using System.Net;
using System.Text;
using System.Text.Json;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Infrastructure.Identity;

namespace umbral_backend.Infrastructure.IntegrationTests.Identity;

public sealed class ParticipantMembershipAccessClientTests
{
    [Fact]
    public async Task ValidateAsync_WhenUsersReturnsDecision_ParsesPayloadAndForwardsTrustedHeaders()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            return CreateJsonResponse(new
            {
                capability = "ParticipantExperience",
                isAllowed = true,
                reasonCode = "eligible",
                reason = "Participant membership validated.",
                liveSessionId,
                teamId
            });
        });

        var client = CreateClient(handler);

        var result = await client.ValidateAsync(liveSessionId, teamId, "join-token", CancellationToken.None);

        result.Should().Be(new ParticipantMembershipAccessDecisionDto(
            "ParticipantExperience",
            true,
            "eligible",
            "Participant membership validated.",
            liveSessionId,
            teamId));

        handler.Requests.Should().ContainSingle();
        var request = handler.Requests.Single();
        request.RequestUri!.PathAndQuery.Should().Be("/api/permissions/participant-membership-access");
        request.Headers.GetValues("X-User-Id").Should().ContainSingle().Which.Should().Be("kc-participant-01");
        request.Headers.GetValues("X-User-Role").Should().ContainSingle().Which.Should().Be("Participant");
        request.Headers.GetValues("X-User-Email").Should().ContainSingle().Which.Should().Be("participant@example.com");

        requestBody.Should().Contain(liveSessionId.ToString());
        requestBody.Should().Contain(teamId.ToString());
        requestBody.Should().Contain("join-token");
    }

    [Fact]
    public async Task ValidateAsync_WhenUsersReturnsFailureStatus_FailsClosed()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = CreateClient(handler);

        var result = await client.ValidateAsync(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("users-unavailable");
        result.Reason.Should().Contain("503");
    }

    [Fact]
    public async Task ValidateAsync_WhenUsersIsUnavailable_FailsClosed()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var result = await client.ValidateAsync(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("users-unavailable");
        result.Reason.Should().Contain("unavailable");
    }

    private static ParticipantMembershipAccessClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://identity-access-service")
        };

        return new ParticipantMembershipAccessClient(
            httpClient,
            new StubCurrentUser("kc-participant-01", "participant@example.com", "Participant"));
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

    private sealed record StubCurrentUser(string? Id, string? Email, string? Role) : umbral_backend.Application.Common.Interfaces.ICurrentUser;

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
