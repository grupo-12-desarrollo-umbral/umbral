namespace umbral_backend.Domain.Events;

public sealed class RankingRefreshed : BaseEvent
{
    public RankingRefreshed(Guid rankingId, Guid liveSessionId, DateTimeOffset generatedAt, long calculationVersion)
    {
        RankingId = rankingId;
        LiveSessionId = liveSessionId;
        GeneratedAt = generatedAt;
        CalculationVersion = calculationVersion;
    }

    public Guid RankingId { get; }

    public Guid LiveSessionId { get; }

    public DateTimeOffset GeneratedAt { get; }

    public long CalculationVersion { get; }
}
