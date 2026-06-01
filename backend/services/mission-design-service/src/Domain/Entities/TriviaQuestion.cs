using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class TriviaQuestion : BaseEntity
{
    private readonly List<TriviaOption> _options = [];

    private TriviaQuestion()
    {
        Prompt = string.Empty;
    }

    private TriviaQuestion(string prompt, int sequenceOrder, bool isActive, IEnumerable<TriviaOption> options)
    {
        Prompt = prompt;
        SequenceOrder = sequenceOrder;
        IsActive = isActive;
        _options.AddRange(options);
    }

    public string Prompt { get; private set; }

    public int SequenceOrder { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<TriviaOption> Options => _options.AsReadOnly();

    public static TriviaQuestion Create(
        string prompt,
        int sequenceOrder,
        IEnumerable<TriviaOption>? options = null,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new TriviaQuestionPromptRequiredException();
        }

        if (sequenceOrder <= 0)
        {
            throw new TriviaQuestionSequenceOrderMustBePositiveException();
        }

        return new TriviaQuestion(
            prompt.Trim(),
            sequenceOrder,
            isActive,
            options ?? []);
    }
}
