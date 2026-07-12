using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;

public sealed class UpdateTriviaQuestionCommandHandler : IRequestHandler<UpdateTriviaQuestionCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public UpdateTriviaQuestionCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(UpdateTriviaQuestionCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.TriviaQuizId, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.TriviaQuizId);

        if (triviaQuiz.Questions.All(question => question.Id != request.QuestionId))
        {
            throw new NotFoundException("TriviaQuestion", request.QuestionId);
        }

        var options = TriviaAuthoringInputMapper.MapOptions(request.Options);

        triviaQuiz.UpdateQuestion(
            request.QuestionId,
            request.Prompt,
            request.ScoreValue,
            request.TimeLimitSeconds,
            request.Explanation,
            options,
            request.IsActive);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
