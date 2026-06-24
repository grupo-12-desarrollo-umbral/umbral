using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;

public sealed class DuplicateTriviaQuizCommandHandler : IRequestHandler<DuplicateTriviaQuizCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public DuplicateTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task<TriviaQuizDto> Handle(DuplicateTriviaQuizCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        var duplicate = triviaQuiz.Duplicate();

        await _triviaQuizRepository.AddAsync(duplicate, cancellationToken);

        return TriviaQuizDtoMapper.Map(duplicate);
    }
}
