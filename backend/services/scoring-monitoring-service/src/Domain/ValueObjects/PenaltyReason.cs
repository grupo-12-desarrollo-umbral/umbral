using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class PenaltyReason : ValueObject
{
    private PenaltyReason(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PenaltyReason Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PenaltyRequiresReasonException(value);
        }

        return new PenaltyReason(value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
