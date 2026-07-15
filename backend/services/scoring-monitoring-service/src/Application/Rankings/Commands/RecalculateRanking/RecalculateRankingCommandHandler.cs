using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Rankings.Commands.RecalculateRanking;

public sealed class RecalculateRankingCommandHandler : IRequestHandler<RecalculateRankingCommand>
{
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IRankingRepository _rankingRepository;
    private readonly IRankingPolicy _rankingPolicy;

    public RecalculateRankingCommandHandler(
        IScoreEntryRepository scoreEntryRepository,
        IRankingRepository rankingRepository,
        IRankingPolicy rankingPolicy)
    {
        _scoreEntryRepository = scoreEntryRepository;
        _rankingRepository = rankingRepository;
        _rankingPolicy = rankingPolicy;
    }

    public async Task Handle(RecalculateRankingCommand request, CancellationToken cancellationToken)
    {
        var scoreEntries = await _scoreEntryRepository.ListByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);
        var ranking = await _rankingRepository.GetByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);

        if (ranking is null)
        {
            ranking = Ranking.Create(
                request.LiveSessionId,
                scoreEntries,
                request.GeneratedAt,
                calculationVersion: 1,
                _rankingPolicy);
        }
        else
        {
            ranking.Refresh(
                scoreEntries,
                request.GeneratedAt,
                ranking.CalculationVersion + 1,
                _rankingPolicy);
        }

        await _rankingRepository.SaveAsync(ranking, cancellationToken);
    }
}
