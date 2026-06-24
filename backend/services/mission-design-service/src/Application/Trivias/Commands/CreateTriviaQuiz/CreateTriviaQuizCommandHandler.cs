using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;

public sealed class CreateTriviaQuizCommandHandler : IRequestHandler<CreateTriviaQuizCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public CreateTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(CreateTriviaQuizCommand request, CancellationToken cancellationToken)
    {
        var questions = TriviaAuthoringInputMapper.MapQuestions(request.Questions);
        var triviaQuiz = TriviaQuiz.Create(request.Title, request.Description, questions);

        await _triviaQuizRepository.AddAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
