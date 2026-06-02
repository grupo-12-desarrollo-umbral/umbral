namespace umbral_backend.Application.Trivias.Common.Authoring;

public abstract class TriviaQuestionAuthoringCommandValidator<TCommand> : AbstractValidator<TCommand>
    where TCommand : ITriviaQuestionAuthoringCommand
{
    private const int MinimumScoreValue = 1;
    private const int MaximumScoreValue = 100;
    private const int MinimumTimeLimitSeconds = 5;
    private const int MaximumTimeLimitSeconds = 120;
    private const int MaximumPromptLength = 2000;
    private const int MaximumOptionTextLength = 1000;
    private const int MaximumExplanationLength = 4000;

    protected TriviaQuestionAuthoringCommandValidator()
    {
        RuleFor(command => command.TriviaQuizId)
            .GreaterThan(0);

        RuleFor(command => command.Prompt)
            .NotEmpty()
            .MaximumLength(MaximumPromptLength);

        RuleFor(command => command.SequenceOrder)
            .GreaterThan(0);

        RuleFor(command => command.ScoreValue)
            .InclusiveBetween(MinimumScoreValue, MaximumScoreValue);

        RuleFor(command => command.TimeLimitSeconds)
            .InclusiveBetween(MinimumTimeLimitSeconds, MaximumTimeLimitSeconds);

        RuleFor(command => command.Explanation)
            .MaximumLength(MaximumExplanationLength);

        RuleFor(command => command.Options)
            .Must(options => options.Count is >= 2 and <= 4)
            .WithMessage("A trivia question must define between two and four options.")
            .Must(HaveExactlyOneCorrectOption)
            .WithMessage("A trivia question must declare exactly one correct option.")
            .Must(HaveDistinctOptionSequenceOrders)
            .WithMessage("Option sequence orders must be unique within the question.");

        RuleForEach(command => command.Options)
            .SetValidator(new TriviaOptionInputValidator(MaximumOptionTextLength));

        AddOperationSpecificRules();
    }

    protected virtual void AddOperationSpecificRules()
    {
    }

    private static bool HaveExactlyOneCorrectOption(IReadOnlyCollection<TriviaOptionInput> options)
    {
        return options.Count(option => option.IsCorrect) == 1;
    }

    private static bool HaveDistinctOptionSequenceOrders(IReadOnlyCollection<TriviaOptionInput> options)
    {
        return options.Count == options.Select(option => option.SequenceOrder).Distinct().Count();
    }

    private sealed class TriviaOptionInputValidator : AbstractValidator<TriviaOptionInput>
    {
        public TriviaOptionInputValidator(int maximumOptionTextLength)
        {
            RuleFor(option => option.OptionText)
                .NotEmpty()
                .MaximumLength(maximumOptionTextLength);

            RuleFor(option => option.SequenceOrder)
                .GreaterThan(0);
        }
    }
}
