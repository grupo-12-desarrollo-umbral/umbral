using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;

/// <summary>
/// Third trivia-specific link (last): first-write-wins. Exactly one accepted answer per team per snapshotted
/// question; a repeat is rejected as a duplicate with the same reason the domain skeleton raises, so
/// late and duplicate rejections stay consistent. Resolves the answering team by runtime or reference
/// id (matching the aggregate); an unknown team is left to the domain's team-resolution guard.
/// </summary>
public sealed class DuplicateTriviaAnswerLink : TriviaAnswerValidationLink
{
    protected override Task CheckAsync(TriviaAnswerValidationContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;

        var team = session.Teams.SingleOrDefault(team =>
            team.TeamId == context.TeamId ||
            team.ReferenceTeamId == context.TeamId);

        if (team is null)
        {
            // Team resolution is not this link's concern — the domain skeleton throws for it.
            return Task.CompletedTask;
        }

        var alreadyAnswered = session.TriviaAnswerSubmissions.Any(answer =>
            answer.TeamId == team.TeamId &&
            answer.ActiveSubstageId == context.TriviaSubstageSnapshotId &&
            answer.QuestionSequenceOrder == context.QuestionSequenceOrder);

        if (alreadyAnswered)
        {
            throw new DuplicateTriviaAnswerException(team.TeamId, context.QuestionSequenceOrder);
        }

        return Task.CompletedTask;
    }
}
