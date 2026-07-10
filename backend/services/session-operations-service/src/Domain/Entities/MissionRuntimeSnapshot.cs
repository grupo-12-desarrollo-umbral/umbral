using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class MissionRuntimeSnapshot : BaseEntity
{
    private readonly List<StageSnapshot> _stageSnapshots = [];
    private readonly List<TargetSnapshot> _targetSnapshots = [];
    private readonly List<TriviaQuestionSnapshot> _triviaQuestionSnapshots = [];

    private MissionRuntimeSnapshot()
    {
        MissionRuntimeSnapshotId = Guid.Empty;
        SourceMissionId = Guid.Empty;
        MissionTitle = string.Empty;
        MaximumTime = null!;
    }

    private MissionRuntimeSnapshot(
        Guid missionRuntimeSnapshotId,
        Guid sourceMissionId,
        string missionTitle,
        MaximumTime maximumTime,
        IEnumerable<StageSnapshot> stageSnapshots,
        IEnumerable<TargetSnapshot> targetSnapshots,
        IEnumerable<TriviaQuestionSnapshot> triviaQuestionSnapshots)
    {
        var normalizedStages = stageSnapshots?.ToArray() ?? [];
        var normalizedTargets = targetSnapshots?.ToArray() ?? [];
        var normalizedQuestions = triviaQuestionSnapshots?.ToArray() ?? [];

        if (sourceMissionId == Guid.Empty)
        {
            throw new SessionSourceEntityRequiredException();
        }

        if (normalizedStages.Length == 0)
        {
            throw new MissionRuntimeSnapshotMustContainStagesException();
        }

        MissionRuntimeSnapshotId = missionRuntimeSnapshotId;
        SourceMissionId = sourceMissionId;
        MissionTitle = missionTitle.Trim();
        MaximumTime = maximumTime;

        EnsureStrictStageOrder(normalizedStages);
        EnsureUniqueTargetQrCodes(normalizedTargets);
        EnsureSubstageInvariants(normalizedStages, normalizedTargets, normalizedQuestions);

        _stageSnapshots.AddRange(normalizedStages);
        _targetSnapshots.AddRange(normalizedTargets);
        _triviaQuestionSnapshots.AddRange(normalizedQuestions);
    }

    public Guid MissionRuntimeSnapshotId { get; private set; }

    public Guid SourceMissionId { get; private set; }

    public string MissionTitle { get; private set; }

    public MaximumTime MaximumTime { get; private set; }

    public IReadOnlyCollection<StageSnapshot> StageSnapshots => _stageSnapshots.AsReadOnly();

    public IReadOnlyCollection<TargetSnapshot> TargetSnapshots => _targetSnapshots.AsReadOnly();

    public IReadOnlyCollection<TriviaQuestionSnapshot> TriviaQuestionSnapshots => _triviaQuestionSnapshots.AsReadOnly();

    public static MissionRuntimeSnapshot Create(
        Guid sourceMissionId,
        string missionTitle,
        MaximumTime maximumTime,
        IEnumerable<StageSnapshot> stageSnapshots,
        IEnumerable<TargetSnapshot> targetSnapshots,
        IEnumerable<TriviaQuestionSnapshot> triviaQuestionSnapshots)
    {
        return new MissionRuntimeSnapshot(
            Guid.NewGuid(),
            sourceMissionId,
            missionTitle,
            maximumTime,
            stageSnapshots,
            targetSnapshots,
            triviaQuestionSnapshots);
    }

    private static void EnsureStrictStageOrder(IEnumerable<StageSnapshot> stageSnapshots)
    {
        var ordered = stageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index].SequenceOrder != index + 1)
            {
                throw new MissionRuntimeSnapshotStageOrderInvalidException();
            }
        }
    }

    private static void EnsureUniqueTargetQrCodes(IEnumerable<TargetSnapshot> targetSnapshots)
    {
        var duplicates = targetSnapshots
            .GroupBy(target => target.QrCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new MissionRuntimeSnapshotTargetQrCodesMustBeUniqueException();
        }
    }

    private static void EnsureSubstageInvariants(
        IEnumerable<StageSnapshot> stageSnapshots,
        IReadOnlyCollection<TargetSnapshot> targetSnapshots,
        IReadOnlyCollection<TriviaQuestionSnapshot> triviaQuestionSnapshots)
    {
        foreach (var stage in stageSnapshots)
        {
            foreach (var substage in stage.SubstageSnapshots)
            {
                if (substage.PlayMode == SubstagePlayMode.TreasureHunt)
                {
                    var substageTargets = targetSnapshots
                        .Where(target => target.SubstageSnapshotId == substage.SubstageSnapshotId)
                        .ToArray();

                    if (substageTargets.Length == 0)
                    {
                        throw new TreasureHuntSubstageSnapshotMustContainTargetsException();
                    }

                    if (substageTargets.Any(target => target.Score is null || target.Score.Value <= 0))
                    {
                        throw new TreasureHuntTargetSnapshotScoreRequiredException();
                    }

                    continue;
                }

                if (!triviaQuestionSnapshots.Any(question => question.SubstageSnapshotId == substage.SubstageSnapshotId))
                {
                    throw new TriviaSubstageSnapshotMustContainQuestionsException();
                }
            }
        }
    }
}
