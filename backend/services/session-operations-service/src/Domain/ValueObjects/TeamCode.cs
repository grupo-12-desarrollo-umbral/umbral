using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class TeamCode : ValueObject
{
    private TeamCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static TeamCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new TeamCodeRequiredException();
        }

        return new TeamCode(value.Trim().ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
