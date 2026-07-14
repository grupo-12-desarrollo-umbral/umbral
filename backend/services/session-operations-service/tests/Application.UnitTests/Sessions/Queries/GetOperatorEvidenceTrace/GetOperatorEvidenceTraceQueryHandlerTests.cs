using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Queries.GetOperatorEvidenceTrace;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetOperatorEvidenceTrace;

public sealed class GetOperatorEvidenceTraceQueryHandlerTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenAssignedOperator_ReturnsTraceList()
    {
        var session = CreateMinimalSession();
        var entries = new List<EvidenceTraceEntry>
        {
            EvidenceTraceEntry.ForRegistration(
                Guid.NewGuid(),
                session.LiveSessionId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                EvidenceSubmissionType.TriviaAnswer,
                Guid.NewGuid(),
                "question:1",
                SubmittedAt),
            EvidenceTraceEntry.ForRegistration(
                Guid.NewGuid(),
                session.LiveSessionId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                EvidenceSubmissionType.TreasureHuntQrScan,
                Guid.NewGuid(),
                "target:abc",
                SubmittedAt),
        };
        // Mark one as accepted for coverage of all fields.
        var resolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        entries[0].MarkAccepted(resolvedAt);

        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver.Setup(r => r.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.ListBySessionAsync(session.LiveSessionId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var handler = new GetOperatorEvidenceTraceQueryHandler(resolver.Object, repo.Object);

        var result = await handler.Handle(
            new GetOperatorEvidenceTraceQuery(session.LiveSessionId),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.Items.Should().HaveCount(2);
        result.Items[0].ValidationState.Should().Be(EvidenceValidationState.Accepted.ToString());
        result.Items[0].RejectionReason.Should().BeNull();
        result.Items[0].ResolvedAt.Should().Be(resolvedAt);
        result.Items[0].SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer.ToString());
        result.Items[1].ValidationState.Should().Be(EvidenceValidationState.Pending.ToString());
    }

    [Fact]
    public async Task Handle_WithTeamIdFilter_FiltersByTeam()
    {
        var session = CreateMinimalSession();
        var teamId = Guid.NewGuid();
        var entries = new List<EvidenceTraceEntry>
        {
            EvidenceTraceEntry.ForRegistration(
                Guid.NewGuid(),
                session.LiveSessionId,
                teamId,
                Guid.NewGuid(),
                EvidenceSubmissionType.TriviaAnswer,
                Guid.NewGuid(),
                "question:1",
                SubmittedAt),
        };

        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver.Setup(r => r.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.ListBySessionAsync(session.LiveSessionId, teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var handler = new GetOperatorEvidenceTraceQueryHandler(resolver.Object, repo.Object);

        var result = await handler.Handle(
            new GetOperatorEvidenceTraceQuery(session.LiveSessionId, teamId),
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].TeamId.Should().Be(teamId);
    }

    [Fact]
    public async Task Handle_WhenNonOwningOperator_ThrowsForbiddenAccessException()
    {
        var liveSessionId = Guid.NewGuid();
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver.Setup(r => r.GetAuthorizedSessionAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        var repo = new Mock<IEvidenceTraceRepository>();
        var handler = new GetOperatorEvidenceTraceQueryHandler(resolver.Object, repo.Object);

        var act = async () => await handler.Handle(
            new GetOperatorEvidenceTraceQuery(liveSessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_NoAdHocRoleCheck_InHandlerBody()
    {
        // The handler delegates access entirely to the resolver (the Proxy).
        // This test confirms no ad-hoc role/owner if leaked into the handler.
        var session = CreateMinimalSession();
        var emptyEntries = Array.Empty<EvidenceTraceEntry>().ToList();

        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver.Setup(r => r.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.ListBySessionAsync(session.LiveSessionId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyEntries);

        var handler = new GetOperatorEvidenceTraceQueryHandler(resolver.Object, repo.Object);

        var result = await handler.Handle(
            new GetOperatorEvidenceTraceQuery(session.LiveSessionId),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        // If an ad-hoc check had leaked in and rejected, this would have thrown.
    }

    [Fact]
    public void Query_IsDecoratedWithAuthorizeAttribute()
    {
        var attributes = typeof(GetOperatorEvidenceTraceQuery)
            .GetCustomAttributes(typeof(umbral_backend.Application.Common.Security.AuthorizeAttribute), inherit: false)
            .Cast<umbral_backend.Application.Common.Security.AuthorizeAttribute>()
            .ToList();

        attributes.Should().ContainSingle(a => a.Roles == "Operator");
    }

    private static LiveSession CreateMinimalSession()
    {
        // Use reflection to create a LiveSession with minimal fields for the resolver to return.
        // The handler only reads LiveSessionId, so a skeleton is sufficient.
        var session = (LiveSession)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(LiveSession));

        var idField = typeof(LiveSession).GetProperty(nameof(LiveSession.LiveSessionId))!;
        idField.SetValue(session, Guid.NewGuid());

        return session;
    }
}
