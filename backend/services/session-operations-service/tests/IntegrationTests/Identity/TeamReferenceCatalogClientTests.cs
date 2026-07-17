using System.Text;
using System.Text.Json;
using umbral_backend.Infrastructure.Identity;

namespace umbral_backend.Infrastructure.IntegrationTests.Identity;

public sealed class TeamReferenceCatalogClientTests
{
    private static readonly Guid TeamId = Guid.Parse("6f1d3c9e-2b47-4a58-9c31-8ad0f5e27b14");

    [Fact]
    public async Task GetByIdAsync_WhenTeamExists_ReturnsReferenceWithParticipantCountAndForwardsTrustedHeaders()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == $"/api/teams/{TeamId}/participants")
            {
                return CreateJsonResponse(new[]
                {
                    CreateParticipantPayload("member-one@example.com", "Member One"),
                    CreateParticipantPayload("member-two@example.com", "Member Two")
                });
            }

            if (request.RequestUri.AbsolutePath == $"/api/teams/{TeamId}")
            {
                return CreateJsonResponse(CreateTeamPayload());
            }

            throw new InvalidOperationException("Unexpected request URI.");
        });

        var client = CreateClient(handler);

        var result = await client.GetByIdAsync(TeamId, CancellationToken.None);

        result.Should().Be(new TeamReferenceDto(TeamId, "Alpha Squad", "ALPHA-01", true, 2));

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be($"/api/teams/{TeamId}");
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Be($"/api/teams/{TeamId}/participants");

        // Both calls cross the trust boundary, so both must carry the actor headers.
        foreach (var request in handler.Requests)
        {
            request.Headers.GetValues("X-User-Id").Should().ContainSingle().Which.Should().Be("kc-operator-01");
            request.Headers.GetValues("X-User-Role").Should().ContainSingle().Which.Should().Be("Operator");
            request.Headers.GetValues("X-User-Email").Should().ContainSingle().Which.Should().Be("operator@example.com");
        }
    }

    [Fact]
    public async Task GetByIdAsync_WhenTeamIsInactive_ReturnsReferencePreservingInactiveFlag()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/participants", StringComparison.Ordinal)
            ? CreateJsonResponse(Array.Empty<object>())
            : CreateJsonResponse(CreateTeamPayload(isActive: false)));

        var client = CreateClient(handler);

        var result = await client.GetByIdAsync(TeamId, CancellationToken.None);

        result!.IsActive.Should().BeFalse();
        result.ParticipantCount.Should().Be(0);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTeamIsNotFound_ReturnsNullWithoutFetchingParticipants()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var client = CreateClient(handler);

        var result = await client.GetByIdAsync(TeamId, CancellationToken.None);

        result.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByIdAsync_WhenTeamLookupFails_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var client = CreateClient(handler);

        var act = () => client.GetByIdAsync(TeamId, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenParticipantLookupFails_ThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/participants", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : CreateJsonResponse(CreateTeamPayload()));

        var client = CreateClient(handler);

        var act = () => client.GetByIdAsync(TeamId, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenTeamPayloadIsEmpty_ThrowsInvalidOperationException()
    {
        // A literal `null` body deserializes to null on a 200 — distinct from the 404 contract above.
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse("null"));

        var client = CreateClient(handler);

        var act = () => client.GetByIdAsync(TeamId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*empty team reference payload for team '{TeamId}'*");
    }

    [Fact]
    public async Task GetByIdAsync_WhenParticipantPayloadIsEmpty_ThrowsInvalidOperationException()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/participants", StringComparison.Ordinal)
            ? CreateJsonResponse("null")
            : CreateJsonResponse(CreateTeamPayload()));

        var client = CreateClient(handler);

        var act = () => client.GetByIdAsync(TeamId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*empty team participant payload for team '{TeamId}'*");
    }

    [Fact]
    public async Task GetByIdAsync_WhenCurrentUserIsAnonymous_OmitsTrustedHeaders()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/participants", StringComparison.Ordinal)
            ? CreateJsonResponse(Array.Empty<object>())
            : CreateJsonResponse(CreateTeamPayload()));

        var client = CreateClient(handler, new StubCurrentUser(null, null, null));

        await client.GetByIdAsync(TeamId, CancellationToken.None);

        foreach (var request in handler.Requests)
        {
            request.Headers.Contains("X-User-Id").Should().BeFalse();
            request.Headers.Contains("X-User-Role").Should().BeFalse();
            request.Headers.Contains("X-User-Email").Should().BeFalse();
        }
    }

    private static object CreateTeamPayload(bool isActive = true)
    {
        return new
        {
            teamId = TeamId,
            displayName = "Alpha Squad",
            teamCode = "ALPHA-01",
            isActive,
            createdAt = DateTimeOffset.Parse("2026-01-05T10:00:00Z"),
            updatedAt = DateTimeOffset.Parse("2026-01-06T11:30:00Z")
        };
    }

    private static object CreateParticipantPayload(string email, string displayName)
    {
        return new
        {
            teamMembershipId = Guid.NewGuid(),
            teamId = TeamId,
            userId = 27,
            email,
            displayName,
            assignedAt = DateTimeOffset.Parse("2026-01-07T09:15:00Z")
        };
    }

    private static TeamReferenceCatalogClient CreateClient(
        StubHttpMessageHandler handler,
        StubCurrentUser? currentUser = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://identity-access-service")
        };

        return new TeamReferenceCatalogClient(
            httpClient,
            currentUser ?? new StubCurrentUser("kc-operator-01", "operator@example.com", "Operator"));
    }

    private static HttpResponseMessage CreateJsonResponse(object payload)
    {
        var json = payload as string ?? JsonSerializer.Serialize(payload);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
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
