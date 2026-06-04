using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Commands.CreateTriviaSession;

public sealed class CreateTriviaSessionFacade : ICreateTriviaSessionFacade
{
    private const string PublishedStatus = "Published";

    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IPublishedTriviaQuizSource _publishedTriviaQuizSource;

    public CreateTriviaSessionFacade(
        ILiveSessionRepository liveSessionRepository,
        IPublishedTriviaQuizSource publishedTriviaQuizSource)
    {
        _liveSessionRepository = liveSessionRepository;
        _publishedTriviaQuizSource = publishedTriviaQuizSource;
    }

    public async Task<CreateTriviaSessionResultDto> CreateAsync(
        CreateTriviaSessionCommand command,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await _publishedTriviaQuizSource.GetByIdAsync(command.SourceTriviaQuizId, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", command.SourceTriviaQuizId);

        if (!string.Equals(triviaQuiz.Status, PublishedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new SourceTriviaQuizNotPublishedException(command.SourceTriviaQuizId, triviaQuiz.Status);
        }

        var snapshot = TriviaSessionSnapshot.Create(
            triviaQuiz.Title,
            triviaQuiz.Questions
                .Where(question => question.IsActive)
                .OrderBy(question => question.SequenceOrder)
                .Select(question => TriviaQuestionSnapshot.Create(
                    question.Prompt,
                    question.SequenceOrder,
                    question.ScoreValue,
                    question.TimeLimitSeconds,
                    question.Explanation,
                    question.Options
                        .OrderBy(option => option.SequenceOrder)
                        .Select(option => TriviaOptionSnapshot.Create(
                            option.OptionText,
                            option.SequenceOrder,
                            option.IsCorrect))
                        .ToArray()))
                .ToArray());

        var liveSession = LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(command.SourceTriviaQuizId),
            GenerateSessionCode(),
            command.Title,
            command.MaximumTimeMinutes,
            command.ScheduledAt,
            snapshot);

        // Sessions are created unassigned; an administrator assigns the responsible
        // operator afterward via the operator-assignment endpoint (HU-19).
        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new CreateTriviaSessionResultDto(
            liveSession.LiveSessionId,
            liveSession.SessionCode,
            liveSession.TitleSnapshot,
            liveSession.State.ToString(),
            liveSession.ScheduledAt,
            command.SourceTriviaQuizId,
            snapshot.Questions.Count);
    }

    private static string GenerateSessionCode()
    {
        return $"SES-{Guid.NewGuid():N}"[..12];
    }
}
