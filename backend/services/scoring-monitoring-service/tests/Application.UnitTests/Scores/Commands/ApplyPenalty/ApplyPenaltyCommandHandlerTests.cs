using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Scores;
using umbral_backend.Application.Scores.Commands.ApplyPenalty;
using umbral_backend.Application.Scores.Common.Authorization;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.Commands.ApplyPenalty;

public sealed class ApplyPenaltyCommandHandlerTests
{
    private readonly Mock<IScoringSessionAccessResolver> _accessResolver = new();
    private readonly Mock<IPenaltyPolicy> _penaltyPolicy = new();
    private readonly Mock<IScorePolicy> _scorePolicy = new();
    private readonly Mock<IScorePolicySelector> _scorePolicySelector = new();
    private readonly Mock<IScoreEntryRepository> _scoreEntryRepository = new();
    private readonly Mock<IPenaltyRepository> _penaltyRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public ApplyPenaltyCommandHandlerTests()
    {
        _currentUser.SetupGet(u => u.Id).Returns(Guid.NewGuid().ToString());
        _scorePolicySelector
            .Setup(selector => selector.For(ScoreSourceType.Penalty))
            .Returns(_scorePolicy.Object);
    }

    private ApplyPenaltyCommandHandler CreateHandler() => new(
        _accessResolver.Object,
        _penaltyPolicy.Object,
        _scorePolicySelector.Object,
        _scoreEntryRepository.Object,
        _penaltyRepository.Object,
        _currentUser.Object);

    [Fact]
    public async Task Handle_WhenPenaltyIsValid_PersistsBothEntitiesAndReturnsDto()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var reason = "Unsportsmanlike conduct";
        var deductionValue = ScoreValue.Create(100);

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _scorePolicy
            .Setup(p => p.Award(It.IsAny<ScoreValue>(), It.IsAny<int>()))
            .Returns(deductionValue);

        ScoreEntry? savedEntry = null;
        _scoreEntryRepository
            .Setup(r => r.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreEntry, CancellationToken>((entry, _) => savedEntry = entry)
            .Returns(Task.CompletedTask);

        Penalty? savedPenalty = null;
        _penaltyRepository
            .Setup(r => r.AddAsync(It.IsAny<Penalty>(), It.IsAny<CancellationToken>()))
            .Callback<Penalty, CancellationToken>((penalty, _) => savedPenalty = penalty)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new ApplyPenaltyCommand(liveSessionId, teamId, reason),
            CancellationToken.None);

        _accessResolver.Verify(
            r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()),
            Times.Once);

        _penaltyPolicy.Verify(
            p => p.ValidateEligibility(liveSessionId, teamId, reason),
            Times.Once);

        _scorePolicy.Verify(
            p => p.Award(It.IsAny<ScoreValue>(), It.IsAny<int>()),
            Times.Once);

        _scoreEntryRepository.Verify(
            r => r.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _penaltyRepository.Verify(
            r => r.AddAsync(It.IsAny<Penalty>(), It.IsAny<CancellationToken>()),
            Times.Once);

        savedEntry.Should().NotBeNull();
        savedEntry!.EntryType.Should().Be(Domain.Enums.ScoreEntryType.Penalty);
        savedEntry.ScoreValue.Should().Be(deductionValue);
        savedEntry.LiveSessionId.Should().Be(liveSessionId);
        savedEntry.TeamId.Should().Be(teamId);
        savedEntry.ReasonCode.Should().Be(reason);
        savedEntry.DomainEvents.OfType<ScoreEntryRegistered>().Should().ContainSingle()
            .Which.EntryType.Should().Be(Domain.Enums.ScoreEntryType.Penalty);

        savedPenalty.Should().NotBeNull();
        savedPenalty!.ScoreEntryId.Should().Be(savedEntry.ScoreEntryId);

        result.Should().NotBeNull();
        result.ScoreEntryId.Should().Be(savedEntry.ScoreEntryId);
        result.TeamId.Should().Be(teamId);
        result.PenaltyAmount.Should().Be(deductionValue.Value);
        result.Reason.Should().Be(reason);
        result.AppliedAt.Should().BeAfter(DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task Handle_WhenAccessResolverThrowsForbiddenAccessException_DoesNotPersist()
    {
        var liveSessionId = Guid.NewGuid();
        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        var handler = CreateHandler();

        var act = () => handler.Handle(
            new ApplyPenaltyCommand(liveSessionId, Guid.NewGuid(), "Reason"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();

        _penaltyPolicy.Verify(
            p => p.ValidateEligibility(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);

        _scoreEntryRepository.Verify(
            r => r.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _penaltyRepository.Verify(
            r => r.AddAsync(It.IsAny<Penalty>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPenaltyIsNotEligible_ThrowsPenaltyNotEligibleException()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var reason = "Invalid";

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _penaltyPolicy
            .Setup(p => p.ValidateEligibility(liveSessionId, teamId, reason))
            .Throws(new PenaltyNotEligibleException(liveSessionId, teamId, reason));

        var handler = CreateHandler();

        var act = () => handler.Handle(
            new ApplyPenaltyCommand(liveSessionId, teamId, reason),
            CancellationToken.None);

        await act.Should().ThrowAsync<PenaltyNotEligibleException>();

        _scoreEntryRepository.Verify(
            r => r.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _penaltyRepository.Verify(
            r => r.AddAsync(It.IsAny<Penalty>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAdministrator_PassesThroughProxyAndPersists()
    {
        // The Proxy passes for Administrators; the handler should complete normally.
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var reason = "Penalty by admin";

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _scorePolicy
            .Setup(p => p.Award(It.IsAny<ScoreValue>(), It.IsAny<int>()))
            .Returns(ScoreValue.Create(100));

        ScoreEntry? savedEntry = null;
        _scoreEntryRepository
            .Setup(r => r.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreEntry, CancellationToken>((entry, _) => savedEntry = entry)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new ApplyPenaltyCommand(liveSessionId, teamId, reason),
            CancellationToken.None);

        result.Should().NotBeNull();
        savedEntry.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_RespectsStrategyScorePolicy_InjectsAndCallsPolicy()
    {
        var liveSessionId = Guid.NewGuid();
        var customDeduction = ScoreValue.Create(75);

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _scorePolicy
            .Setup(p => p.Award(It.IsAny<ScoreValue>(), It.IsAny<int>()))
            .Returns(customDeduction);

        ScoreEntry? savedEntry = null;
        _scoreEntryRepository
            .Setup(r => r.AddAsync(It.IsAny<ScoreEntry>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreEntry, CancellationToken>((entry, _) => savedEntry = entry)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new ApplyPenaltyCommand(liveSessionId, Guid.NewGuid(), "Valid reason"),
            CancellationToken.None);

        savedEntry.Should().NotBeNull();
        result.PenaltyAmount.Should().Be(75);
        _scorePolicy.Verify(p => p.Award(It.IsAny<ScoreValue>(), It.IsAny<int>()), Times.Once);
    }
}
