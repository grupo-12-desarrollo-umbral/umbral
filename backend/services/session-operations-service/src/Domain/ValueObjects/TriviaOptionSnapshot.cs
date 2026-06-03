namespace umbral_backend.Domain.ValueObjects;

public sealed class TriviaOptionSnapshot : ValueObject
{
    private TriviaOptionSnapshot(string optionText, int sequenceOrder, bool isCorrect)
    {
        OptionText = optionText.Trim();
        SequenceOrder = sequenceOrder;
        IsCorrect = isCorrect;
    }

    public string OptionText { get; }

    public int SequenceOrder { get; }

    public bool IsCorrect { get; }

    public static TriviaOptionSnapshot Create(string optionText, int sequenceOrder, bool isCorrect)
    {
        return new TriviaOptionSnapshot(optionText, sequenceOrder, isCorrect);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return OptionText;
        yield return SequenceOrder;
        yield return IsCorrect;
    }
}
