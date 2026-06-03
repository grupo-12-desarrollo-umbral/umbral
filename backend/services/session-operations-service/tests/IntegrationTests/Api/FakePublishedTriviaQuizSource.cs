using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class FakePublishedTriviaQuizSource : IPublishedTriviaQuizSource
{
    public PublishedTriviaQuizDto? Quiz { get; set; }

    public Task<PublishedTriviaQuizDto?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            Quiz is not null && Quiz.Id == triviaQuizId
                ? Quiz
                : null);
    }
}
