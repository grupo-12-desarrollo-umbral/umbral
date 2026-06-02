using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Common.Authoring;

public abstract class TriviaQuestionAuthoringCommandHandler<TCommand> : IRequestHandler<TCommand, TriviaQuizDto>
    where TCommand : ITriviaQuestionAuthoringCommand
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    protected TriviaQuestionAuthoringCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.TriviaQuizId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("TriviaQuiz", request.TriviaQuizId);

        EnsureOperationTargetExists(triviaQuiz, request);

        var options = request.Options
            .OrderBy(option => option.SequenceOrder)
            .Select(option => TriviaOption.Create(option.OptionText, option.SequenceOrder, option.IsCorrect))
            .ToArray();

        ApplyQuestionAuthoring(triviaQuiz, request, options);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }

    protected virtual void EnsureOperationTargetExists(TriviaQuiz triviaQuiz, TCommand request)
    {
    }

    protected abstract void ApplyQuestionAuthoring(
        TriviaQuiz triviaQuiz,
        TCommand request,
        IReadOnlyCollection<TriviaOption> options);
}
