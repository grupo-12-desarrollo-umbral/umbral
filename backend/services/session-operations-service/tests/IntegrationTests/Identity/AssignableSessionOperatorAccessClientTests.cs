using System.Net;
using System.Text;
using System.Text.Json;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Infrastructure.Identity;

namespace umbral_backend.Infrastructure.IntegrationTests.Identity;

public sealed class AssignableSessionOperatorAccessClientTests
{
    [Fact]
    public async Task GetEligibilityAsync_WhenCatalogContainsOperator_ReturnsEligibleAndForwardsTrustedHeaders()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(new
        {
            items = new[]
            {
                new
                {
                    id = 27,
                    externalIdentityId = "kc-operator-27",
                    displayName = "Operator 27",
                    email = "operator27@example.com",
                    role = "Operator",
                    isActive = true
                }
            },
            totalCount = 1,
            page = 1,
            pageSize = 100
        }));

        var client = CreateClient(handler);

        var result = await client.GetEligibilityAsync(27, CancellationToken.None);

        result.Should().Be(new SessionOperatorEligibilityDecisionDto(
            "users-service.user-catalog",
            true,
            27,
            "Operator",
            null,
            "kc-operator-27"));

        handler.Requests.Should().ContainSingle();
        var request = handler.Requests.Single();
        request.RequestUri!.PathAndQuery.Should().Be("/api/users?page=1&pageSize=100");
        request.Headers.GetValues("X-User-Id").Should().ContainSingle().Which.Should().Be("kc-admin-01");
        request.Headers.GetValues("X-User-Role").Should().ContainSingle().Which.Should().Be("Administrator");
        request.Headers.GetValues("X-User-Email").Should().ContainSingle().Which.Should().Be("admin@example.com");
    }

    [Fact]
    public async Task GetEligibilityAsync_WhenUserIsInactiveParticipant_ReturnsIneligibleDecision()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(new
        {
            items = new[]
            {
                new
                {
                    id = 42,
                    externalIdentityId = "kc-participant-42",
                    displayName = "Participant 42",
                    email = "participant42@example.com",
                    role = "Participant",
                    isActive = false
                }
            },
            totalCount = 1,
            page = 1,
            pageSize = 100
        }));

        var client = CreateClient(handler);

        var result = await client.GetEligibilityAsync(42, CancellationToken.None);

        result.IsEligible.Should().BeFalse();
        result.OperatorUserId.Should().Be(42);
        result.Role.Should().Be("Participant");
        result.Reason.Should().Be("User is deactivated in the identity access catalog.");
    }

    [Fact]
    public async Task GetEligibilityAsync_WhenUserIsNotOnFirstPage_KeepsPagingUntilMatch()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            return request.RequestUri!.Query switch
            {
                "?page=1&pageSize=100" => CreateJsonResponse(new
                {
                    items = Array.Empty<object>(),
                    totalCount = 101,
                    page = 1,
                    pageSize = 100
                }),
                "?page=2&pageSize=100" => CreateJsonResponse(new
                {
                    items = new[]
                    {
                        new
                        {
                            id = 155,
                            externalIdentityId = "kc-admin-155",
                            displayName = "Admin 155",
                            email = "admin155@example.com",
                            role = "Administrator",
                            isActive = true
                        }
                    },
                    totalCount = 101,
                    page = 2,
                    pageSize = 100
                }),
                _ => throw new InvalidOperationException("Unexpected request URI.")
            };
        });

        var client = CreateClient(handler);

        var result = await client.GetEligibilityAsync(155, CancellationToken.None);

        result.IsEligible.Should().BeTrue();
        result.Role.Should().Be("Administrator");
        handler.Requests.Should().HaveCount(2);
    }

    private static AssignableSessionOperatorAccessClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://users-service")
        };

        return new AssignableSessionOperatorAccessClient(
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
