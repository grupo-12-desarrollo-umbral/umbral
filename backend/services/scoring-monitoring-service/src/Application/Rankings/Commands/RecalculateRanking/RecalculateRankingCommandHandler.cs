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

        // Team names ride on each score entry (snapshotted at record time from the integration event),
        // so ranking rows are named without an authenticated cross-service lookup that this user-less
        // consumer context cannot make. Keyed by the entry's TeamId (the cross-context ReferenceTeamId).
        var teamNames = scoreEntries
            .GroupBy(entry => entry.TeamId)
            .ToDictionary(
                group => group.Key,
                // Prefer the most recent NON-EMPTY name. Entries pre-dating the team-name column
                // (e.g. directly-seeded rows) carry a blank name and must not win over a real one.
                group => group
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.TeamDisplayName))
                    .OrderByDescending(entry => entry.RecordedAt)
                    .Select(entry => entry.TeamDisplayName)
                    .FirstOrDefault() ?? group.Key.ToString());

        var ranking = await _rankingRepository.GetByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);

        if (ranking is null)
        {
            ranking = Ranking.Create(
                request.LiveSessionId,
                scoreEntries,
                request.GeneratedAt,
                calculationVersion: 1,
                _rankingPolicy,
                teamNames);
        }
        else
        {
            ranking.Refresh(
                scoreEntries,
                request.GeneratedAt,
                ranking.CalculationVersion + 1,
                _rankingPolicy,
                teamNames);
        }

        await _rankingRepository.SaveAsync(ranking, cancellationToken);
    }
}
