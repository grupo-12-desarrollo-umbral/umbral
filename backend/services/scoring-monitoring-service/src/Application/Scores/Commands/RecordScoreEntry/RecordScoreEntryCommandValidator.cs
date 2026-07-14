namespace umbral_backend.Application.Scores.Commands.RecordScoreEntry;

public sealed class RecordScoreEntryCommandValidator : AbstractValidator<RecordScoreEntryCommand>
{
    public RecordScoreEntryCommandValidator()
    {
        RuleFor(command => command.LiveSessionId)
            .NotEmpty();

        RuleFor(command => command.TeamId)
            .NotEmpty();

        RuleFor(command => command.ReasonCode)
            .NotEmpty();

        RuleFor(command => command.ScoreValue)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.SourceEntityType)
            .IsInEnum();

        RuleFor(command => command.SourceEntityId)
            .NotEmpty();
    }
}
