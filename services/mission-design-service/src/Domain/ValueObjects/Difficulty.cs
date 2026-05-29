using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class Difficulty : ValueObject
{
    private Difficulty()
    {
        Value = string.Empty;
    }

    private Difficulty(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static Difficulty Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DifficultyValueRequiredException();
        }

        return new Difficulty(value.Trim());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
