using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Scores;
using umbral_backend.Application.Scores.Common.Authorization;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Scores.Commands.ApplyPenalty;

public sealed class ApplyPenaltyCommandHandler : IRequestHandler<ApplyPenaltyCommand, AppliedPenaltyDto>
{
    private static readonly ScoreValue BasePenaltyMagnitude = ScoreValue.Create(100);

    private readonly IScoringSessionAccessResolver _accessResolver;
    private readonly IPenaltyPolicy _penaltyPolicy;
    private readonly IScorePolicySelector _scorePolicySelector;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IPenaltyRepository _penaltyRepository;
    private readonly ICurrentUser _currentUser;

    public ApplyPenaltyCommandHandler(
        IScoringSessionAccessResolver accessResolver,
        IPenaltyPolicy penaltyPolicy,
        IScorePolicySelector scorePolicySelector,
        IScoreEntryRepository scoreEntryRepository,
        IPenaltyRepository penaltyRepository,
        ICurrentUser currentUser)
    {
        _accessResolver = accessResolver;
        _penaltyPolicy = penaltyPolicy;
        _scorePolicySelector = scorePolicySelector;
        _scoreEntryRepository = scoreEntryRepository;
        _penaltyRepository = penaltyRepository;
        _currentUser = currentUser;
    }

    public async Task<AppliedPenaltyDto> Handle(ApplyPenaltyCommand request, CancellationToken cancellationToken)
    {
        await _accessResolver.EnsureAccessAsync(request.LiveSessionId, cancellationToken);

        var appliedByUserId = Guid.Parse(_currentUser.Id!);

        _penaltyPolicy.ValidateEligibility(request.LiveSessionId, request.TeamId, request.Reason);

        var deductionValue = _scorePolicySelector
            .For(ScoreSourceType.Penalty)
            .Award(BasePenaltyMagnitude, difficultyFactor: 1);

        var scoreEntryId = Guid.NewGuid();
        var penalty = Penalty.Create(scoreEntryId, request.Reason, appliedByUserId);

        // The Penalty is the single source of the applied-at instant: threading its AppliedAt into the
        // ledger factory keeps the persisted row and ScoreEntryRegistered on one timestamp.
        var scoreEntry = ScoreEntry.Penalty(
            scoreEntryId,
            request.LiveSessionId,
            request.TeamId,
            string.Empty,
            request.Reason,
            deductionValue,
            penalty.PenaltyId,
            penalty.AppliedAt);

        await _scoreEntryRepository.AddAsync(scoreEntry, cancellationToken);
        await _penaltyRepository.AddAsync(penalty, cancellationToken);

        return new AppliedPenaltyDto(
            scoreEntry.ScoreEntryId,
            request.TeamId,
            deductionValue.Value,
            request.Reason,
            penalty.AppliedAt);
    }
}
