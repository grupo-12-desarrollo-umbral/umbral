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
        return new TriviaOption(
            ValidateOptionText(optionText),
            ValidateSequenceOrder(sequenceOrder),
            isCorrect);
    }

    internal void ApplyAuthoring(string optionText, int sequenceOrder, bool isCorrect)
    {
        OptionText = ValidateOptionText(optionText);
        SequenceOrder = ValidateSequenceOrder(sequenceOrder);
        IsCorrect = isCorrect;
    }

    private static string ValidateOptionText(string optionText)
    {
        if (string.IsNullOrWhiteSpace(optionText))
        {
            throw new TriviaOptionTextRequiredException();
        }

        return optionText.Trim();
    }

    private static int ValidateSequenceOrder(int sequenceOrder)
    {
        if (sequenceOrder <= 0)
        {
            throw new TriviaOptionSequenceOrderMustBePositiveException();
        }

        return sequenceOrder;
    }
}
