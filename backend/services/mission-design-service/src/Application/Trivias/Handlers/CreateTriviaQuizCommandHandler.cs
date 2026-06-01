using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class CreateTriviaQuizCommandHandler : TriviaQuizAuthoringCommandHandler<CreateTriviaQuizCommand>
{
    public CreateTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository)
        : base(triviaQuizRepository)
    {
    }

    protected override Task<TriviaQuiz> ApplyWorkflowAsync(
        CreateTriviaQuizCommand request,
        IReadOnlyCollection<TriviaQuestion> questions,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(TriviaQuiz.Create(request.Title, request.Description, questions));
    }

    protected override Task PersistAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return AddAsync(triviaQuiz, cancellationToken);
    }
}
