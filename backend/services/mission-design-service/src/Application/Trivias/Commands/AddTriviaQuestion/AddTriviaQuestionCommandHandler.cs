using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;

public sealed class AddTriviaQuestionCommandHandler : IRequestHandler<AddTriviaQuestionCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public AddTriviaQuestionCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(AddTriviaQuestionCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.TriviaQuizId, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.TriviaQuizId);

        var options = TriviaAuthoringInputMapper.MapOptions(request.Options);

        triviaQuiz.AddQuestion(
            request.Prompt,
            request.SequenceOrder,
            request.ScoreValue,
            request.TimeLimitSeconds,
            request.Explanation,
            options,
            request.IsActive);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
