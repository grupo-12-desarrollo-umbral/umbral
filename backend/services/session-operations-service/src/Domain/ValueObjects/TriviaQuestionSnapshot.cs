using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class TriviaQuestionSnapshot : ValueObject
{
    private readonly List<TriviaOptionSnapshot> _options = [];

    private TriviaQuestionSnapshot()
    {
        Prompt = string.Empty;
    }

    private TriviaQuestionSnapshot(
        string prompt,
        int sequenceOrder,
        int scoreValue,
        int timeLimitSeconds,
        string? explanation,
        IEnumerable<TriviaOptionSnapshot> options)
    {
        var normalizedOptions = options?.ToArray() ?? [];
        if (normalizedOptions.Length < 2)
        {
            throw new TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException();
        }

        if (!normalizedOptions.Any(option => option.IsCorrect))
        {
            throw new TriviaQuestionSnapshotRequiresCorrectOptionException();
        }

        Prompt = prompt.Trim();
        SequenceOrder = sequenceOrder;
        ScoreValue = scoreValue;
        TimeLimitSeconds = timeLimitSeconds;
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        _options.AddRange(normalizedOptions);
    }

    public string Prompt { get; }

    public int SequenceOrder { get; }

    public int ScoreValue { get; }

    public int TimeLimitSeconds { get; }

    public string? Explanation { get; }

    public IReadOnlyCollection<TriviaOptionSnapshot> Options => _options.AsReadOnly();

    public static TriviaQuestionSnapshot Create(
        string prompt,
        int sequenceOrder,
        int scoreValue,
        int timeLimitSeconds,
        string? explanation,
        IEnumerable<TriviaOptionSnapshot> options)
    {
        return new TriviaQuestionSnapshot(prompt, sequenceOrder, scoreValue, timeLimitSeconds, explanation, options);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Prompt;
        yield return SequenceOrder;
        yield return ScoreValue;
        yield return TimeLimitSeconds;
        yield return Explanation;

        foreach (var option in _options)
        {
            yield return option;
        }
    }
}
