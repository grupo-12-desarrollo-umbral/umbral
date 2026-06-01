using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class UpdateTriviaQuizCommandHandler : TriviaQuizAuthoringCommandHandler<UpdateTriviaQuizCommand>
{
    public UpdateTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository)
        : base(triviaQuizRepository)
    {
    }

    protected override async Task<TriviaQuiz> ApplyWorkflowAsync(
        UpdateTriviaQuizCommand request,
        IReadOnlyCollection<TriviaQuestion> questions,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        triviaQuiz.UpdateDetails(request.Title, request.Description, questions);

        return triviaQuiz;
    }

    protected override Task PersistAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return UpdateAsync(triviaQuiz, cancellationToken);
    }
}
