using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class MissionReadModelRepository : IMissionReadModelRepository
{
    private readonly ApplicationDbContext _context;

    public MissionReadModelRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MissionSummaryDto>> GetMissionCatalogAsync(CancellationToken cancellationToken)
    {
        return await _context.Missions
            .AsNoTracking()
            .OrderByDescending(mission => mission.LastModified)
            .Select(mission => new MissionSummaryDto(
                mission.Id,
                mission.Name,
                mission.Description,
                mission.Difficulty.Value,
                mission.ActivationState.ToString()))
            .ToListAsync(cancellationToken);
    }

    public Task<MissionDto?> GetMissionDetailAsync(int missionId, CancellationToken cancellationToken)
    {
        return _context.Missions
            .AsNoTracking()
            .Where(mission => mission.Id == missionId)
            .Select(mission => mission)
            .SingleOrDefaultAsync(cancellationToken)
            .ContinueWith(
                task => task.Result is null ? null : Application.Missions.Common.MissionDtoMapper.Map(task.Result),
                cancellationToken);
    }

    public async Task<MissionRuntimePlanDto?> GetMissionRuntimePlanAsync(int missionId, CancellationToken cancellationToken)
    {
        var mission = await _context.Missions
            .AsNoTracking()
            .SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);

        if (mission is null)
        {
            return null;
        }

        var triviaQuizIds = mission.Stages
            .SelectMany(stage => stage.Substages)
            .Where(substage => substage.PlayMode == SubstagePlayMode.Trivia && substage.TriviaQuizId.HasValue)
            .Select(substage => substage.TriviaQuizId!.Value)
            .Distinct()
            .ToArray();

        var triviaQuestionsByQuizId = await LoadTriviaQuestionsByQuizIdAsync(triviaQuizIds, cancellationToken);

        return new MissionRuntimePlanDto(
            mission.Name,
            mission.MaximumTime.Minutes,
            mission.Stages
                .Select(stage => new MissionRuntimePlanStageDto(
                    stage.Title,
                    stage.SequenceOrder,
                    stage.Substages
                        .Select(substage => MapSubstage(substage, triviaQuestionsByQuizId))
                        .ToList()))
                .ToList());
    }

    private async Task<Dictionary<int, IReadOnlyList<MissionRuntimePlanTriviaQuestionDto>>> LoadTriviaQuestionsByQuizIdAsync(
        IReadOnlyCollection<int> triviaQuizIds,
        CancellationToken cancellationToken)
    {
        if (triviaQuizIds.Count == 0)
        {
            return [];
        }

        // Resolve every selected quiz in one batched read to avoid per-substage lookups.
        var triviaQuizzes = await _context.TriviaQuizzes
            .AsNoTracking()
            .Where(triviaQuiz => triviaQuizIds.Contains(triviaQuiz.Id) && triviaQuiz.Status == TriviaQuizStatus.Published)
            .Include(triviaQuiz => triviaQuiz.Questions)
            .ThenInclude(question => question.Options)
            .ToListAsync(cancellationToken);

        return triviaQuizzes.ToDictionary(
            triviaQuiz => triviaQuiz.Id,
            triviaQuiz => (IReadOnlyList<MissionRuntimePlanTriviaQuestionDto>)triviaQuiz.Questions
                .OrderBy(question => question.SequenceOrder)
                .Select(MapTriviaQuestion)
                .ToList());
    }

    private static MissionRuntimePlanSubstageDto MapSubstage(
        Substage substage,
        IReadOnlyDictionary<int, IReadOnlyList<MissionRuntimePlanTriviaQuestionDto>> triviaQuestionsByQuizId)
    {
        var cluesById = substage.Clues.ToDictionary(clue => clue.Id);
        var triviaQuestions = substage.TriviaQuizId is int triviaQuizId
            && triviaQuestionsByQuizId.TryGetValue(triviaQuizId, out var resolvedQuestions)
                ? resolvedQuestions
                : Array.Empty<MissionRuntimePlanTriviaQuestionDto>();

        return new MissionRuntimePlanSubstageDto(
            substage.Title,
            substage.SequenceOrder,
            substage.PlayMode.ToString(),
            substage.WinnerScore?.Points,
            substage.Targets
                .Select(target => MapTarget(target, cluesById))
                .ToList(),
            triviaQuestions);
    }

    private static MissionRuntimePlanTargetDto MapTarget(
        Target target,
        IReadOnlyDictionary<int, Clue> cluesById)
    {
        return new MissionRuntimePlanTargetDto(
            target.Name,
            target.QrCode,
            target.SequenceOrder,
            target.IsActive,
            target.ClueId is int clueId && cluesById.TryGetValue(clueId, out var clue)
                ? new MissionRuntimePlanClueDto(clue.Text, clue.Visibility.ToString())
                : null);
    }

    private static MissionRuntimePlanTriviaQuestionDto MapTriviaQuestion(TriviaQuestion question)
    {
        var scoreValue = question.ScoreValue
            ?? throw new InvalidOperationException("Published trivia questions must define ScoreValue.");
        var timeLimitSeconds = question.TimeLimit?.Seconds
            ?? throw new InvalidOperationException("Published trivia questions must define TimeLimitSeconds.");

        return new MissionRuntimePlanTriviaQuestionDto(
            question.Prompt,
            question.SequenceOrder,
            question.Options
                .OrderBy(option => option.SequenceOrder)
                .Select(option => new MissionRuntimePlanTriviaOptionDto(
                    option.OptionText,
                    option.SequenceOrder,
                    option.IsCorrect))
                .ToList(),
            scoreValue,
            timeLimitSeconds);
    }
}
