using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class GetTriviaDetailQueryHandler : IRequestHandler<GetTriviaDetailQuery, TriviaQuizDto>
{
    private readonly ITriviaQuizReadModelRepository _triviaQuizReadModelRepository;

    public GetTriviaDetailQueryHandler(ITriviaQuizReadModelRepository triviaQuizReadModelRepository)
    {
        _triviaQuizReadModelRepository = triviaQuizReadModelRepository;
    }

    public async Task<TriviaQuizDto> Handle(GetTriviaDetailQuery request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizReadModelRepository.GetTriviaDetailAsync(request.Id, cancellationToken);

        if (triviaQuiz is null)
        {
            throw new NotFoundException("TriviaQuiz", request.Id);
        }

        return triviaQuiz;
    }
}
