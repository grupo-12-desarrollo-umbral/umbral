using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.RegisterTargetScan;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation.Validators;
using umbral_backend.Application.Sessions.Common.TargetResolution;
using umbral_backend.Application.Sessions.Common.TargetResolution.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.RegisterTargetScan;

public sealed class RegisterTargetScanCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidActiveTarget_AcceptsPersistsAndRaisesBothFacts()
    {
        var setup = Setup(targetInActiveSubstage: true);

        var result = await setup.Handler.Handle(
            new(setup.Session.LiveSessionId, setup.TeamId, " qr-target "), CancellationToken.None);

        result.IsResolved.Should().BeTrue();
        result.TargetSnapshotId.Should().NotBeNull();
        result.RejectionReason.Should().BeNull();
        setup.Session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().ContainSingle();
        setup.Session.DomainEvents.OfType<TargetResolvedEvent>().Should().ContainSingle();
        setup.Repository.Verify(repository => repository.UpdateAsync(
            setup.Session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownQr_RetainsRejectedScanAndPersistsRegistrationFact()
    {
        var setup = Setup(targetInActiveSubstage: true);

        var result = await setup.Handler.Handle(
            new(setup.Session.LiveSessionId, setup.TeamId, "unknown"), CancellationToken.None);

        result.IsResolved.Should().BeFalse();
        result.RejectionReason.Should().Be(
            TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget.ToMessage());
        setup.Session.TreasureEvidenceSubmissions.Should().ContainSingle();
        setup.Session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().ContainSingle();
        setup.Session.DomainEvents.OfType<TargetResolvedEvent>().Should().BeEmpty();
        setup.Repository.Verify(repository => repository.UpdateAsync(
            setup.Session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TargetOutsideActiveSubstage_RetainsOwnershipRejection()
    {
        var setup = Setup(targetInActiveSubstage: false);

        var result = await setup.Handler.Handle(
            new(setup.Session.LiveSessionId, setup.TeamId, "QR-TARGET"), CancellationToken.None);

        result.IsResolved.Should().BeFalse();
        result.RejectionReason.Should().Be(
            TargetResolutionRejectionReason.TargetOutsideActiveSubstage.ToMessage());
        setup.Session.DomainEvents.OfType<TargetResolvedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AlreadyResolvedTarget_RetainsDuplicateRejection()
    {
        var setup = Setup(targetInActiveSubstage: true);
        setup.Session.RegisterTargetScan(setup.TeamId, "QR-TARGET", Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = await setup.Handler.Handle(
            new(setup.Session.LiveSessionId, setup.TeamId, "QR-TARGET"), CancellationToken.None);

        result.IsResolved.Should().BeFalse();
        result.RejectionReason.Should().Be(
            TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam.ToMessage());
        setup.Session.TreasureEvidenceSubmissions.Should().HaveCount(2);
        setup.Session.DomainEvents.OfType<TargetResolvedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_RuntimeParticipationDenied_BlocksBeforeRegistration()
    {
        var setup = Setup(targetInActiveSubstage: true, runtimeAllowed: false);

        await FluentActions.Awaiting(() => setup.Handler.Handle(
                new(setup.Session.LiveSessionId, setup.TeamId, "QR-TARGET"), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenAccessException>();
        setup.Session.TreasureEvidenceSubmissions.Should().BeEmpty();
        setup.Repository.Verify(repository => repository.UpdateAsync(
            It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingSession_ThrowsNotFound()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository.Setup(candidate => candidate.GetByIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var setup = Setup(targetInActiveSubstage: true, repositoryOverride: repository);

        await FluentActions.Awaiting(() => setup.Handler.Handle(
                new(Guid.NewGuid(), setup.TeamId, "QR-TARGET"), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    private static HandlerSetup Setup(
        bool targetInActiveSubstage,
        bool runtimeAllowed = true,
        Mock<ILiveSessionRepository>? repositoryOverride = null)
    {
        var session = CreateActiveSession(targetInActiveSubstage, out var teamId, out var externalIdentityId);
        var repository = repositoryOverride ?? new Mock<ILiveSessionRepository>();
        repository.Setup(candidate => candidate.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository.Setup(candidate => candidate.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var guard = new Mock<IRuntimeParticipationGuard>();
        var guardSetup = guard.Setup(candidate => candidate.EnsureAllowedAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()));
        if (runtimeAllowed) guardSetup.Returns(Task.CompletedTask);
        else guardSetup.ThrowsAsync(new ForbiddenAccessException());

        var evidenceChain = new EvidenceIntakeValidationChain(new EvidenceIntakeValidationLink[]
        {
            new RuntimeParticipationLink(guard.Object),
            new SessionAdmitsReceptionLink(),
            new ActiveSubstagePresentLink()
        });
        var targetChain = new TargetResolutionChain(new TargetResolutionLink[]
        {
            new TargetExistsForScanLink(),
            new TargetBelongsToActiveSubstageLink(),
            new TargetNotAlreadyResolvedLink()
        });
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(candidate => candidate.Id).Returns(externalIdentityId.ToString());
        var handler = new RegisterTargetScanCommandHandler(
            repository.Object,
            targetChain,
            new EvidenceIntakeFacade(evidenceChain, repository.Object),
            currentUser.Object,
            new FixedTimeProvider(DateTimeOffset.UtcNow));
        return new(session, teamId, repository, handler);
    }

    private static LiveSession CreateActiveSession(
        bool targetInActiveSubstage,
        out Guid teamId,
        out Guid externalIdentityId)
    {
        var first = SubstageSnapshot.CreateTreasureHunt("First", 1);
        var second = SubstageSnapshot.CreateTreasureHunt("Second", 2);
        var scannedTargetSubstageId = targetInActiveSubstage
            ? first.SubstageSnapshotId
            : second.SubstageSnapshotId;
        var otherSubstageId = targetInActiveSubstage
            ? second.SubstageSnapshotId
            : first.SubstageSnapshotId;
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(), "Hunt", MaximumTime.Create(45),
            [StageSnapshot.Create("Stage", 1, [first, second])],
            [
                TargetSnapshot.Create(scannedTargetSubstageId, "Target", "QR-TARGET", 1, true, 150, 0, 0, null, null),
                TargetSnapshot.Create(otherSubstageId, "Other", "QR-OTHER", 1, true, 100, 0, 0, null, null)
            ],
            []);
        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId), "hunt-1", "Hunt", 45,
            DateTimeOffset.UtcNow, snapshot);
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow.AddMinutes(-1), policy);
        externalIdentityId = Guid.NewGuid();
        session.AdmitParticipant(
            externalIdentityId, "Alice", team.TeamId, DateTimeOffset.UtcNow.AddSeconds(-30), new JoinPolicy());
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow, policy);
        teamId = team.TeamId;
        return session;
    }

    private sealed record HandlerSetup(
        LiveSession Session,
        Guid TeamId,
        Mock<ILiveSessionRepository> Repository,
        RegisterTargetScanCommandHandler Handler);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
