using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class Ranking : BaseAuditableEntity
{
    private readonly List<Row> _rows = new();

    private Ranking()
    {
        RankingId = Guid.Empty;
    }

    private Ranking(Guid rankingId, Guid liveSessionId)
    {
        RankingId = rankingId;
        LiveSessionId = liveSessionId;
    }

    public Guid RankingId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public DateTimeOffset GeneratedAt { get; private set; }

    public long CalculationVersion { get; private set; }

    public IReadOnlyCollection<Row> Rows => _rows.AsReadOnly();

    public static Ranking Create(
        Guid liveSessionId,
        IEnumerable<ScoreEntry> entries,
        DateTimeOffset generatedAt,
        long calculationVersion,
        IRankingPolicy rankingPolicy,
        IReadOnlyDictionary<Guid, string>? teamNames = null)
    {
        var ranking = new Ranking(Guid.NewGuid(), liveSessionId);
        ranking.Refresh(entries, generatedAt, calculationVersion, rankingPolicy, teamNames);
        return ranking;
    }

    public void Refresh(
        IEnumerable<ScoreEntry> entries,
        DateTimeOffset generatedAt,
        long calculationVersion,
        IRankingPolicy rankingPolicy,
        IReadOnlyDictionary<Guid, string>? teamNames = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(rankingPolicy);

        var sessionEntries = entries
            .Where(entry => entry.LiveSessionId == LiveSessionId)
            .ToList();

        var foldedRows = new List<(Guid TeamId, int TotalScore, ResolutionTime ResolutionTime)>();

        if (sessionEntries.Count > 0)
        {
            var sessionStartedAt = sessionEntries.Min(entry => entry.RecordedAt);

            foldedRows.AddRange(sessionEntries
                .GroupBy(entry => entry.TeamId)
                .Select(group => (
                    TeamId: group.Key,
                    // Floor the fold at zero: penalties subtract, so a team penalized before scoring
                    // (or twice below the penalty magnitude) would otherwise fold negative and trip the
                    // Row invariant inside the ranking consumer, dead-lettering recalc for the session.
                    // The floor is a projection rule, not a ledger rule — the ScoreEntry ledger keeps the
                    // full unclamped deduction; only the displayed total bottoms out at 0. Teams clamped
                    // to 0 tie on score and are separated by ResolutionTime.
                    TotalScore: Math.Max(0, group.Sum(entry => entry.EntryType == ScoreEntryType.Penalty
                        ? -entry.ScoreValue.Value
                        : entry.ScoreValue.Value)),
                    ResolutionTime: ResolutionTime.Comparable(
                        group.Max(entry => entry.RecordedAt) - sessionStartedAt))));
        }

        var rankedRows = rankingPolicy.Rank(foldedRows);

        _rows.Clear();
        _rows.AddRange(rankedRows.Select(row =>
        {
            var displayName = teamNames is not null && teamNames.TryGetValue(row.TeamId, out var name)
                ? name
                : row.TeamId.ToString();
            return Row.Create(row.TeamId, row.Position, row.TotalScore, row.ResolutionTime, displayName);
        }));

        GeneratedAt = generatedAt;
        CalculationVersion = calculationVersion;

        AddDomainEvent(new RankingRefreshed(RankingId, LiveSessionId, GeneratedAt, CalculationVersion));
    }

    public sealed class Row : BaseEntity
    {
        private Row()
        {
            ResolutionTime = null!;
            TeamDisplayName = string.Empty;
        }

        private Row(Guid teamId, int position, int totalScore, ResolutionTime resolutionTime, string teamDisplayName)
        {
            if (position <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be positive.");
            }

            if (totalScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalScore), "Total score cannot be negative.");
            }

            ArgumentNullException.ThrowIfNull(resolutionTime);

            TeamId = teamId;
            Position = position;
            TotalScore = totalScore;
            ResolutionTime = resolutionTime;
            TeamDisplayName = teamDisplayName;
        }

        public Guid TeamId { get; private set; }

        public int Position { get; private set; }

        public int TotalScore { get; private set; }

        public ResolutionTime ResolutionTime { get; private set; }

        public string TeamDisplayName { get; private set; }

        internal static Row Create(Guid teamId, int position, int totalScore, ResolutionTime resolutionTime, string teamDisplayName)
        {
            return new Row(teamId, position, totalScore, resolutionTime, teamDisplayName);
        }
    }
}
