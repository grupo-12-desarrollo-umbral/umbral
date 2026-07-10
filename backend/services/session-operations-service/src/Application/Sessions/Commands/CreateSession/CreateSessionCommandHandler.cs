using System.Security.Cryptography;
using System.Text;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Sessions.Commands.CreateSession;

public sealed class CreateSessionCommandHandler
    : IRequestHandler<CreateSessionCommand, CreateSessionResultDto>
{
    private const int SessionCodeLength = 6;

    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IMissionReadinessSource _missionReadinessSource;
    private readonly IMissionRuntimeSource _missionRuntimeSource;
    private readonly SessionCreationPolicy _sessionCreationPolicy;

    public CreateSessionCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        IMissionReadinessSource missionReadinessSource,
        IMissionRuntimeSource missionRuntimeSource,
        SessionCreationPolicy sessionCreationPolicy)
    {
        _liveSessionRepository = liveSessionRepository;
        _missionReadinessSource = missionReadinessSource;
        _missionRuntimeSource = missionRuntimeSource;
        _sessionCreationPolicy = sessionCreationPolicy;
    }

    public async Task<CreateSessionResultDto> Handle(
        CreateSessionCommand request,
        CancellationToken cancellationToken)
    {
        var missionReadiness = await _missionReadinessSource.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);

        _sessionCreationPolicy.EnsureMissionEligible(
            missionReadiness.MissionId,
            missionReadiness.IsActive,
            missionReadiness.IsReady);

        var missionRuntime = await _missionRuntimeSource.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);

        var sourceMissionId = GenerateMissionSourceId(request.MissionId);
        var runtimeSnapshot = BuildMissionRuntimeSnapshot(sourceMissionId, missionRuntime);

        var liveSession = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            GenerateSessionCode(),
            request.Title,
            request.MaximumTimeMinutes,
            request.ScheduledAt,
            runtimeSnapshot);

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new CreateSessionResultDto(
            liveSession.LiveSessionId,
            liveSession.SessionCode,
            liveSession.TitleSnapshot,
            liveSession.State.ToString(),
            liveSession.ScheduledAt);
    }

    private static MissionRuntimeSnapshot BuildMissionRuntimeSnapshot(Guid sourceMissionId, MissionRuntimeDto missionRuntime)
    {
        var stageSnapshots = new List<StageSnapshot>();
        var targetSnapshots = new List<TargetSnapshot>();
        var triviaQuestionSnapshots = new List<TriviaQuestionSnapshot>();

        foreach (var stage in missionRuntime.Stages.OrderBy(stage => stage.SequenceOrder))
        {
            var substageSnapshots = new List<SubstageSnapshot>();

            foreach (var substage in stage.Substages.OrderBy(substage => substage.SequenceOrder))
            {
                var playMode = ParsePlayMode(substage.PlayMode);
                var substageSnapshot = playMode switch
                {
                    SubstagePlayMode.TreasureHunt => SubstageSnapshot.CreateTreasureHunt(
                        substage.Title,
                        substage.SequenceOrder),
                    SubstagePlayMode.Trivia => SubstageSnapshot.CreateTrivia(
                        substage.Title,
                        substage.SequenceOrder),
                    _ => throw InvalidPlayMode(nameof(CreateSessionCommand.MissionId), substage.PlayMode)
                };

                substageSnapshots.Add(substageSnapshot);

                if (playMode == SubstagePlayMode.TreasureHunt)
                {
                    targetSnapshots.AddRange(
                        substage.Targets
                            .OrderBy(target => target.SequenceOrder)
                            .Select(target => TargetSnapshot.Create(
                                substageSnapshot.SubstageSnapshotId,
                                target.Name,
                                target.QrCode,
                                target.SequenceOrder,
                                target.IsActive,
                                target.Score,
                                target.Clue?.Text,
                                target.Clue?.VisibilityPolicy)));

                    continue;
                }

                triviaQuestionSnapshots.AddRange(
                    substage.TriviaQuestions
                        .OrderBy(question => question.SequenceOrder)
                        .Select(question => TriviaQuestionSnapshot.Create(
                            substageSnapshot.SubstageSnapshotId,
                            question.Prompt,
                            question.SequenceOrder,
                            question.ScoreValue,
                            question.TimeLimitSeconds,
                            question.Explanation,
                            question.Options
                                .OrderBy(option => option.SequenceOrder)
                                .Select(option => TriviaOptionSnapshot.Create(
                                    option.OptionText,
                                    option.SequenceOrder,
                                    option.IsCorrect))
                                .ToArray())));
            }

            stageSnapshots.Add(StageSnapshot.Create(stage.Title, stage.SequenceOrder, substageSnapshots));
        }

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            missionRuntime.Title,
            MaximumTime.Create(missionRuntime.MaximumTime),
            stageSnapshots,
            targetSnapshots,
            triviaQuestionSnapshots);
    }

    private static SubstagePlayMode ParsePlayMode(string playMode)
    {
        return Enum.TryParse<SubstagePlayMode>(playMode, ignoreCase: true, out var parsed)
            ? parsed
            : throw InvalidPlayMode(nameof(CreateSessionCommand.MissionId), playMode);
    }

    private static string GenerateSessionCode()
    {
        return Guid.NewGuid().ToString("N")[..SessionCodeLength].ToUpperInvariant();
    }

    private static umbral_backend.Application.Common.Exceptions.ValidationException InvalidPlayMode(string propertyName, string playMode)
    {
        return new umbral_backend.Application.Common.Exceptions.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(propertyName, $"Unsupported substage play mode '{playMode}'.")
            ]);
    }

    // Mission runtime plans are still addressed externally by integer id. Keep the
    // internal mission source identity deterministic until the HTTP adapter lands.
    private static Guid GenerateMissionSourceId(int missionId)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"mission-runtime:{missionId}"));
        return new Guid(hash);
    }
}
