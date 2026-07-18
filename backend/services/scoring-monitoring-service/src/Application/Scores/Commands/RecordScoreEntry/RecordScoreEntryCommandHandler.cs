using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Scores.Commands.RecordScoreEntry;

public sealed class RecordScoreEntryCommandHandler : IRequestHandler<RecordScoreEntryCommand>
{
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IScorePolicySelector _scorePolicySelector;

    public RecordScoreEntryCommandHandler(
        IScoreEntryRepository scoreEntryRepository,
        IScorePolicySelector scorePolicySelector)
    {
        _scoreEntryRepository = scoreEntryRepository;
        _scorePolicySelector = scorePolicySelector;
    }

    public async Task Handle(RecordScoreEntryCommand request, CancellationToken cancellationToken)
    {
        var alreadyRecorded = await _scoreEntryRepository.ExistsForSourceAsync(
            request.SourceEntityType,
            request.SourceEntityId,
            cancellationToken);

        if (alreadyRecorded)
        {
            return;
        }

        var awardedScore = _scorePolicySelector
            .For(request.SourceEntityType)
            .Award(ScoreValue.Create(request.ScoreValue), request.DifficultyFactor);
        var scoreEntry = ScoreEntry.Grant(
            request.LiveSessionId,
            request.TeamId,
            request.TeamDisplayName,
            request.ReasonCode,
            awardedScore,
            request.RecordedAt,
            request.SourceEntityType,
            request.SourceEntityId,
            request.RecordedByUserId);

        await _scoreEntryRepository.AddAsync(scoreEntry, cancellationToken);
    }
}
