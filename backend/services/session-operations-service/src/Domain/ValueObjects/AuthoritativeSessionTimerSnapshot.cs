namespace umbral_backend.Domain.ValueObjects;

public sealed class AuthoritativeSessionTimerSnapshot : ValueObject
{
    private AuthoritativeSessionTimerSnapshot(
        TimeSpan totalDuration,
        TimeSpan remainingDuration,
        bool isAdvancing,
        DateTimeOffset observedAt,
        DateTimeOffset? advancingSince,
        DateTimeOffset? expiredAt)
    {
        if (totalDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDuration));
        }

        if (remainingDuration < TimeSpan.Zero || remainingDuration > totalDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingDuration));
        }

        TotalDuration = totalDuration;
        RemainingDuration = remainingDuration;
        IsAdvancing = isAdvancing;
        ObservedAt = observedAt;
        AdvancingSince = advancingSince;
        ExpiredAt = expiredAt;
    }

    public TimeSpan TotalDuration { get; }

    public TimeSpan RemainingDuration { get; }

    public bool IsAdvancing { get; }

    public bool IsExpired => RemainingDuration <= TimeSpan.Zero;

    public DateTimeOffset ObservedAt { get; }

    public DateTimeOffset? AdvancingSince { get; }

    public DateTimeOffset? ExpiredAt { get; }

    public static AuthoritativeSessionTimerSnapshot Create(
        TimeSpan totalDuration,
        TimeSpan remainingDuration,
        bool isAdvancing,
        DateTimeOffset observedAt,
        DateTimeOffset? advancingSince,
        DateTimeOffset? expiredAt)
    {
        return new AuthoritativeSessionTimerSnapshot(
            totalDuration,
            remainingDuration,
            isAdvancing,
            observedAt,
            advancingSince,
            expiredAt);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TotalDuration;
        yield return RemainingDuration;
        yield return IsAdvancing;
        yield return ObservedAt;
        yield return AdvancingSince;
        yield return ExpiredAt;
    }
}
