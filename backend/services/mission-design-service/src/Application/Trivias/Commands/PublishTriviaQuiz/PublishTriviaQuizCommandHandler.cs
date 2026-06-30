using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;

public sealed class PublishTriviaQuizCommandHandler : IRequestHandler<PublishTriviaQuizCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;
    private readonly IClock _clock;

    public PublishTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository, IClock clock)
    {
        _triviaQuizRepository = triviaQuizRepository;
        _clock = clock;
    }

    public async Task<TriviaQuizDto> Handle(PublishTriviaQuizCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        triviaQuiz.Publish(_clock.UtcNow);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
