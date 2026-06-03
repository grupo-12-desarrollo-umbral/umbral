using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.Integrations.MissionDesign;

/// <summary>
/// HTTP adapter for the published trivia quiz read contract exposed by
/// mission-design-service. Transport mapping only; publication rules stay in
/// the application facade.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PublishedTriviaQuizSource : IPublishedTriviaQuizSource
{
    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public PublishedTriviaQuizSource(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<PublishedTriviaQuizDto?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/trivias/{triviaQuizId}");
        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var triviaQuiz = await response.Content.ReadFromJsonAsync<MissionDesignTriviaQuizResponse>(cancellationToken);
        return triviaQuiz?.ToPublishedTriviaQuizDto();
    }

    private void ForwardTrustedHeaders(HttpRequestMessage requestMessage)
    {
        AddHeaderIfPresent(requestMessage, "X-User-Id", _currentUser.Id);
        AddHeaderIfPresent(requestMessage, "X-User-Role", _currentUser.Role);
        AddHeaderIfPresent(requestMessage, "X-User-Email", _currentUser.Email);
    }

    private static void AddHeaderIfPresent(HttpRequestMessage requestMessage, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            requestMessage.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private sealed record MissionDesignTriviaQuizResponse(
        int Id,
        string Title,
        string Description,
        string Status,
        bool IsSourceReady,
        int? SourceTriviaQuizId,
        bool HasUsageHistory,
        bool IsDuplicate,
        IReadOnlyList<MissionDesignTriviaQuestionResponse> Questions)
    {
        public PublishedTriviaQuizDto ToPublishedTriviaQuizDto()
        {
            return new PublishedTriviaQuizDto(
                Id,
                Title,
                Status,
                Questions
                    .Select(question => new PublishedTriviaQuestionDto(
                        question.Id,
                        question.Prompt,
                        question.SequenceOrder,
                        question.IsActive,
                        question.ScoreValue ?? 0,
                        question.TimeLimitSeconds ?? 0,
                        question.Explanation,
                        question.Options
                            .Select(option => new PublishedTriviaOptionDto(
                                option.Id,
                                option.OptionText,
                                option.SequenceOrder,
                                option.IsCorrect))
                            .ToArray()))
                    .ToArray());
        }
    }

    private sealed record MissionDesignTriviaQuestionResponse(
        int Id,
        string Prompt,
        int SequenceOrder,
        bool IsActive,
        IReadOnlyList<MissionDesignTriviaOptionResponse> Options,
        int? ScoreValue,
        int? TimeLimitSeconds,
        string? Explanation);

    private sealed record MissionDesignTriviaOptionResponse(
        int Id,
        string OptionText,
        int SequenceOrder,
        bool IsCorrect);
}
