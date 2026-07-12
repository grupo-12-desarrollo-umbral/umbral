using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.RemoveTriviaQuestion;

public sealed class RemoveTriviaQuestionCommandHandler : IRequestHandler<RemoveTriviaQuestionCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public RemoveTriviaQuestionCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(RemoveTriviaQuestionCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.TriviaQuizId, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.TriviaQuizId);

        triviaQuiz.RemoveQuestion(request.QuestionId);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
