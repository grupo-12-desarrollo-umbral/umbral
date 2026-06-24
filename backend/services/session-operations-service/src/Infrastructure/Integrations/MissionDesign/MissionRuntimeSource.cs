using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Integrations.MissionDesign;

[ExcludeFromCodeCoverage]
public sealed class MissionRuntimeSource : IMissionRuntimeSource
{
    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public MissionRuntimeSource(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<MissionRuntimeDto?> GetByIdAsync(int missionId, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/missions/{missionId}/runtime-plan");
        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var runtime = await response.Content.ReadFromJsonAsync<MissionRuntimeResponse>(cancellationToken);
        return runtime?.ToMissionRuntimeDto();
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

    private sealed record MissionRuntimeResponse(
        string Title,
        int MaximumTime,
        IReadOnlyList<MissionRuntimeStageResponse> Stages)
    {
        public MissionRuntimeDto ToMissionRuntimeDto()
        {
            return new MissionRuntimeDto(
                Title,
                MaximumTime,
                Stages
                    .Select(stage => stage.ToMissionRuntimeStageDto())
                    .ToArray());
        }
    }

    private sealed record MissionRuntimeStageResponse(
        string Title,
        int SequenceOrder,
        IReadOnlyList<MissionRuntimeSubstageResponse> Substages)
    {
        public MissionRuntimeStageDto ToMissionRuntimeStageDto()
        {
            return new MissionRuntimeStageDto(
                Title,
                SequenceOrder,
                Substages
                    .Select(substage => substage.ToMissionRuntimeSubstageDto())
                    .ToArray());
        }
    }

    private sealed record MissionRuntimeSubstageResponse(
        string Title,
        int SequenceOrder,
        string PlayMode,
        int? WinnerScore,
        IReadOnlyList<MissionRuntimeTargetResponse> Targets,
        IReadOnlyList<MissionRuntimeTriviaQuestionResponse> TriviaQuestions)
    {
        public MissionRuntimeSubstageDto ToMissionRuntimeSubstageDto()
        {
            return new MissionRuntimeSubstageDto(
                Title,
                SequenceOrder,
                PlayMode,
                WinnerScore,
                Targets
                    .Select(target => target.ToMissionRuntimeTargetDto())
                    .ToArray(),
                TriviaQuestions
                    .Select(question => question.ToMissionRuntimeTriviaQuestionDto())
                    .ToArray());
        }
    }

    private sealed record MissionRuntimeTargetResponse(
        string Name,
        string QrCode,
        int SequenceOrder,
        bool IsActive,
        MissionRuntimeClueResponse? Clue)
    {
        public MissionRuntimeTargetDto ToMissionRuntimeTargetDto()
        {
            return new MissionRuntimeTargetDto(
                Name,
                QrCode,
                SequenceOrder,
                IsActive,
                Clue?.ToMissionRuntimeClueDto());
        }
    }

    private sealed record MissionRuntimeClueResponse(
        string Text,
        string VisibilityPolicy)
    {
        public MissionRuntimeClueDto ToMissionRuntimeClueDto()
        {
            return new MissionRuntimeClueDto(Text, VisibilityPolicy);
        }
    }

    private sealed record MissionRuntimeTriviaQuestionResponse(
        string Prompt,
        int SequenceOrder,
        int ScoreValue,
        int TimeLimitSeconds,
        string? Explanation,
        IReadOnlyList<MissionRuntimeTriviaOptionResponse> Options)
    {
        public MissionRuntimeTriviaQuestionDto ToMissionRuntimeTriviaQuestionDto()
        {
            return new MissionRuntimeTriviaQuestionDto(
                Prompt,
                SequenceOrder,
                ScoreValue,
                TimeLimitSeconds,
                Explanation,
                Options
                    .Select(option => option.ToMissionRuntimeTriviaOptionDto())
                    .ToArray());
        }
    }

    private sealed record MissionRuntimeTriviaOptionResponse(
        string OptionText,
        int SequenceOrder,
        bool IsCorrect)
    {
        public MissionRuntimeTriviaOptionDto ToMissionRuntimeTriviaOptionDto()
        {
            return new MissionRuntimeTriviaOptionDto(OptionText, SequenceOrder, IsCorrect);
        }
    }
}
