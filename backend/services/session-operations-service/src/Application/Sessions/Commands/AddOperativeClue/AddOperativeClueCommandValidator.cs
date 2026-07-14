namespace umbral_backend.Application.Sessions.Commands.AddOperativeClue;

public sealed class AddOperativeClueCommandValidator : AbstractValidator<AddOperativeClueCommand>
{
    private const int MaximumClueTextLength = 500;

    public AddOperativeClueCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.ClueText)
            .NotEmpty()
            .MaximumLength(MaximumClueTextLength);

        RuleFor(command => command.TeamIds)
            .NotEmpty();
    }
}
