using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class TriviaOption : BaseEntity
{
    private TriviaOption()
    {
        OptionText = string.Empty;
    }

    private TriviaOption(string optionText, int sequenceOrder, bool isCorrect)
    {
        OptionText = optionText;
        SequenceOrder = sequenceOrder;
        IsCorrect = isCorrect;
    }

    public string OptionText { get; private set; }

    public int SequenceOrder { get; private set; }

    public bool IsCorrect { get; private set; }

    public static TriviaOption Create(string optionText, int sequenceOrder, bool isCorrect)
    {
        if (string.IsNullOrWhiteSpace(optionText))
        {
            throw new TriviaOptionTextRequiredException();
        }

        if (sequenceOrder <= 0)
        {
            throw new TriviaOptionSequenceOrderMustBePositiveException();
        }

        return new TriviaOption(optionText.Trim(), sequenceOrder, isCorrect);
    }
}
