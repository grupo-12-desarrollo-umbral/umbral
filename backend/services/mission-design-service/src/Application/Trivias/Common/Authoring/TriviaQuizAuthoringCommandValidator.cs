namespace umbral_backend.Application.Trivias.Common.Authoring;

public abstract class TriviaQuizAuthoringCommandValidator<TCommand> : AbstractValidator<TCommand>
    where TCommand : ITriviaQuizAuthoringCommand
{
    private const int MaximumTitleLength = 200;
    private const int MaximumDescriptionLength = 2000;
    private const int MaximumPromptLength = 2000;
    private const int MaximumOptionTextLength = 1000;

    protected TriviaQuizAuthoringCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(MaximumTitleLength);

        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(MaximumDescriptionLength);

        RuleForEach(command => command.Questions)
            .SetValidator(new TriviaQuestionInputValidator());

        AddOperationSpecificRules();
    }

    protected virtual void AddOperationSpecificRules()
    {
    }

    private sealed class TriviaQuestionInputValidator : AbstractValidator<TriviaQuestionInput>
    {
        public TriviaQuestionInputValidator()
        {
            RuleFor(question => question.Prompt)
                .NotEmpty()
                .MaximumLength(MaximumPromptLength);

            RuleForEach(question => question.Options)
                .SetValidator(new TriviaOptionInputValidator());

            RuleFor(question => question.Options)
                .Must(HaveDistinctOptionSequenceOrders)
                .WithMessage("Option sequence orders must be unique within the question.");
        }

        private static bool HaveDistinctOptionSequenceOrders(IReadOnlyCollection<TriviaOptionInput> options)
        {
            return options.Count == options.Select(option => option.SequenceOrder).Distinct().Count();
        }
    }

    private sealed class TriviaOptionInputValidator : AbstractValidator<TriviaOptionInput>
    {
        public TriviaOptionInputValidator()
        {
            RuleFor(option => option.OptionText)
                .NotEmpty()
                .MaximumLength(MaximumOptionTextLength);

            RuleFor(option => option.SequenceOrder)
                .GreaterThan(0);
        }
    }
}
