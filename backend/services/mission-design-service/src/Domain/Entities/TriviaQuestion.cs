using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class TriviaQuestion : BaseEntity
{
    internal const int MaximumScoreValue = 100;
    private readonly List<TriviaOption> _options = [];

    private TriviaQuestion()
    {
        Prompt = string.Empty;
    }

    private TriviaQuestion(
        string prompt,
        int sequenceOrder,
        int? scoreValue,
        QuestionTimer? timeLimit,
        string? explanation,
        bool isActive,
        IEnumerable<TriviaOption> options)
    {
        Prompt = prompt;
        SequenceOrder = sequenceOrder;
        ScoreValue = scoreValue;
        TimeLimit = timeLimit;
        Explanation = explanation;
        IsActive = isActive;
        _options.AddRange(options);
    }

    public string Prompt { get; private set; }

    public int SequenceOrder { get; private set; }

    public int? ScoreValue { get; private set; }

    public QuestionTimer? TimeLimit { get; private set; }

    public string? Explanation { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<TriviaOption> Options => _options.AsReadOnly();

    public static TriviaQuestion Create(
        string prompt,
        int sequenceOrder,
        IEnumerable<TriviaOption>? options = null,
        bool isActive = true)
    {
        return Create(
            prompt,
            sequenceOrder,
            null,
            null,
            null,
            options,
            isActive);
    }

    public static TriviaQuestion Create(
        string prompt,
        int sequenceOrder,
        int? scoreValue,
        int? timeLimitSeconds,
        string? explanation,
        IEnumerable<TriviaOption>? options = null,
        bool isActive = true)
    {
        return new TriviaQuestion(
            ValidatePrompt(prompt),
            ValidateSequenceOrder(sequenceOrder),
            ValidateScoreValue(scoreValue),
            CreateTimeLimit(timeLimitSeconds),
            NormalizeExplanation(explanation),
            isActive,
            options ?? []);
    }

    internal void SetSequenceOrder(int sequenceOrder)
    {
        SequenceOrder = ValidateSequenceOrder(sequenceOrder);
    }

    internal void ApplyAuthoring(
        string prompt,
        int sequenceOrder,
        int scoreValue,
        int timeLimitSeconds,
        string? explanation,
        IEnumerable<TriviaOption> options,
        bool isActive)
    {
        Prompt = ValidatePrompt(prompt);
        SequenceOrder = ValidateSequenceOrder(sequenceOrder);
        ScoreValue = ValidateScoreValue(scoreValue);
        TimeLimit = CreateTimeLimit(timeLimitSeconds);
        Explanation = NormalizeExplanation(explanation);
        IsActive = isActive;

        _options.Clear();
        _options.AddRange(options);
    }

    private static string ValidatePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new TriviaQuestionPromptRequiredException();
        }

        return prompt.Trim();
    }

    private static int ValidateSequenceOrder(int sequenceOrder)
    {
        if (sequenceOrder <= 0)
        {
            throw new TriviaQuestionSequenceOrderMustBePositiveException();
        }

        return sequenceOrder;
    }

    private static int? ValidateScoreValue(int? scoreValue)
    {
        if (scoreValue is <= 0)
        {
            throw new TriviaQuestionScoreValueMustBePositiveException();
        }

        if (scoreValue > MaximumScoreValue)
        {
            throw new TriviaQuestionScoreValueExceedsMaximumException();
        }

        return scoreValue;
    }

    private static QuestionTimer? CreateTimeLimit(int? timeLimitSeconds)
    {
        return timeLimitSeconds.HasValue
            ? QuestionTimer.Create(timeLimitSeconds.Value)
            : null;
    }

    private static string? NormalizeExplanation(string? explanation)
    {
        return string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
    }
}
