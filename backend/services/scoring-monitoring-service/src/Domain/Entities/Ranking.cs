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
        IRankingPolicy rankingPolicy)
    {
        var ranking = new Ranking(Guid.NewGuid(), liveSessionId);
        ranking.Refresh(entries, generatedAt, calculationVersion, rankingPolicy);
        return ranking;
    }

    public void Refresh(
        IEnumerable<ScoreEntry> entries,
        DateTimeOffset generatedAt,
        long calculationVersion,
        IRankingPolicy rankingPolicy)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(rankingPolicy);

        var sessionEntries = entries
            .Where(entry => entry.LiveSessionId == LiveSessionId)
            .ToList();

        var foldedRows = new List<(Guid TeamId, int TotalScore, ResolutionTime ResolutionTime)>();

        if (sessionEntries.Count > 0)
        {
            // The ledger is the sole source of the tie-break. A team's resolution time is how long it took
            // them to reach their current total, measured from the session's earliest ledger entry. The
            // baseline is common to every team in the session, so ordering by this elapsed value is
            // equivalent to ordering by the absolute timestamp of each team's most recent entry, while
            // staying a non-negative duration as ResolutionTime requires.
            var sessionStartedAt = sessionEntries.Min(entry => entry.RecordedAt);

            foldedRows.AddRange(sessionEntries
                .GroupBy(entry => entry.TeamId)
                .Select(group => (
                    TeamId: group.Key,
                    TotalScore: group.Sum(entry => entry.ScoreValue.Value),
                    ResolutionTime: ResolutionTime.Comparable(
                        group.Max(entry => entry.RecordedAt) - sessionStartedAt))));
        }

        var rankedRows = rankingPolicy.Rank(foldedRows);

        _rows.Clear();
        _rows.AddRange(rankedRows.Select(row => Row.Create(row.TeamId, row.Position, row.TotalScore, row.ResolutionTime)));

        GeneratedAt = generatedAt;
        CalculationVersion = calculationVersion;

        AddDomainEvent(new RankingRefreshed(RankingId, LiveSessionId, GeneratedAt, CalculationVersion));
    }

    public sealed class Row : BaseEntity
    {
        private Row()
        {
            ResolutionTime = null!;
        }

        private Row(Guid teamId, int position, int totalScore, ResolutionTime resolutionTime)
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
        }

        public Guid TeamId { get; private set; }

        public int Position { get; private set; }

        public int TotalScore { get; private set; }

        public ResolutionTime ResolutionTime { get; private set; }

        internal static Row Create(Guid teamId, int position, int totalScore, ResolutionTime resolutionTime)
        {
            return new Row(teamId, position, totalScore, resolutionTime);
        }
    }
}
