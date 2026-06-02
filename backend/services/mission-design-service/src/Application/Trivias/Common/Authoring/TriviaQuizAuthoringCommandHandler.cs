using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Common.Authoring;

public abstract class TriviaQuizAuthoringCommandHandler<TCommand> : IRequestHandler<TCommand, TriviaQuizDto>
    where TCommand : IRequest<TriviaQuizDto>, ITriviaQuizAuthoringCommand
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    protected TriviaQuizAuthoringCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var questions = MapQuestions(request.Questions);
        var triviaQuiz = await ApplyWorkflowAsync(request, questions, cancellationToken);

        await PersistAsync(triviaQuiz, cancellationToken);

        return MapDto(triviaQuiz);
    }

    protected Task<TriviaQuiz?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken)
    {
        return _triviaQuizRepository.GetByIdAsync(triviaQuizId, cancellationToken);
    }

    protected Task AddAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return _triviaQuizRepository.AddAsync(triviaQuiz, cancellationToken);
    }

    protected Task UpdateAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);
    }

    protected abstract Task<TriviaQuiz> ApplyWorkflowAsync(
        TCommand request,
        IReadOnlyCollection<TriviaQuestion> questions,
        CancellationToken cancellationToken);

    protected abstract Task PersistAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken);

    private static IReadOnlyCollection<TriviaQuestion> MapQuestions(IReadOnlyCollection<TriviaQuestionInput> questions)
    {
        return questions
            .OrderBy(question => question.SequenceOrder)
                .Select(question => TriviaQuestion.Create(
                    question.Prompt,
                    question.SequenceOrder,
                    question.ScoreValue,
                    question.TimeLimitSeconds,
                    question.Explanation,
                    question.Options
                        .OrderBy(option => option.SequenceOrder)
                        .Select(option => TriviaOption.Create(option.OptionText, option.SequenceOrder, option.IsCorrect))
                        .ToArray(),
                    question.IsActive))
            .ToArray();
    }

    private static TriviaQuizDto MapDto(TriviaQuiz triviaQuiz)
    {
        return new TriviaQuizDto(
            triviaQuiz.Id,
            triviaQuiz.Title,
            triviaQuiz.Description,
            triviaQuiz.Status.ToString(),
            triviaQuiz.Questions
                .OrderBy(question => question.SequenceOrder)
                .Select(question => new TriviaQuestionDto(
                    question.Id,
                    question.Prompt,
                    question.SequenceOrder,
                    question.IsActive,
                    question.Options
                        .OrderBy(option => option.SequenceOrder)
                        .Select(option => new TriviaOptionDto(
                            option.Id,
                            option.OptionText,
                            option.SequenceOrder,
                            option.IsCorrect))
                        .ToArray(),
                    question.ScoreValue,
                    question.TimeLimit?.Seconds,
                    question.Explanation))
                .ToArray());
    }
}
