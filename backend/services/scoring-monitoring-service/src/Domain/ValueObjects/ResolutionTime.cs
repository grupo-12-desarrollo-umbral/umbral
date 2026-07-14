namespace umbral_backend.Domain.ValueObjects;

public sealed class ResolutionTime : ValueObject, IComparable<ResolutionTime>
{
    private ResolutionTime(TimeSpan? value)
    {
        Value = value;
    }

    public TimeSpan? Value { get; }

    public bool IsComparable => Value.HasValue;

    public static ResolutionTime Comparable(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Resolution time cannot be negative.");
        }

        return new ResolutionTime(value);
    }

    public static ResolutionTime NonComparable() => new(null);

    public int CompareTo(ResolutionTime? other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!IsComparable || !other.IsComparable)
        {
            return 0;
        }

        return Value!.Value.CompareTo(other.Value!.Value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsComparable;
        yield return Value;
    }
}
