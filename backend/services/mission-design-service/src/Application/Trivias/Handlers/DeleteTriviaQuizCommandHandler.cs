using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class DeleteTriviaQuizCommandHandler : IRequestHandler<DeleteTriviaQuizCommand>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;

    public DeleteTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository)
    {
        _triviaQuizRepository = triviaQuizRepository;
    }

    public async Task Handle(DeleteTriviaQuizCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        triviaQuiz.EnsureCanBeDestructivelyRemoved();

        await _triviaQuizRepository.RemoveAsync(triviaQuiz, cancellationToken);
    }
}
