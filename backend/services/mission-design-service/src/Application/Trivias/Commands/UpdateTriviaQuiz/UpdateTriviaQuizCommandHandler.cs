using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;

public sealed class UpdateTriviaQuizCommandHandler : IRequestHandler<UpdateTriviaQuizCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public UpdateTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(UpdateTriviaQuizCommand request, CancellationToken cancellationToken)
    {
        var questions = TriviaAuthoringInputMapper.MapQuestions(request.Questions);

        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        triviaQuiz.UpdateDetails(request.Title, request.Description, questions);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
