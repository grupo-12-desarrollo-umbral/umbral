using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// Source-content wrapper aggregate for mission authoring. <see cref="Mission"/> is
/// NOT a runtime session: it owns mission metadata and the composite authoring tree
/// (<see cref="Stage"/> -&gt; <see cref="Substage"/> -&gt; <see cref="Clue"/>, with
/// treasure-hunt <see cref="Target"/>s attached to their owning substage). It is the
/// only source from which <c>SessionOperations</c> later creates a <c>LiveSession</c>.
/// Activation/readiness is a source-readiness decision, distinct from the runtime
/// <c>LiveSession</c> lifecycle.
/// </summary>
public sealed class Mission : BaseAuditableEntity
{
    private readonly List<Stage> _stages = [];

    private Mission()
    {
        Name = string.Empty;
        Description = string.Empty;
        Difficulty = null!;
        MaximumTime = null!;
    }

    private Mission(
        string name,
        string description,
        Difficulty difficulty,
        MaximumTime maximumTime,
        bool isActive,
        DateTimeOffset? archivedAt,
        MissionActivation activationState)
    {
        Name = name;
        Description = description;
        Difficulty = difficulty;
        MaximumTime = maximumTime;
        IsActive = isActive;
        ArchivedAt = archivedAt;
        ActivationState = activationState;
    }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public Difficulty Difficulty { get; private set; }

    public MaximumTime MaximumTime { get; private set; }

    public bool IsActive { get; private set; }

    public MissionActivation ActivationState { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Top-level ordered stages — the root of the Composite authoring tree.</summary>
    public IReadOnlyList<Stage> Stages =>
        _stages.OrderBy(stage => stage.SequenceOrder).ToList().AsReadOnly();

    public static Mission Create(string name, string description, string difficulty, int maximumTimeMinutes)
    {
        ValidateName(name);
        ValidateDescription(description);

        var mission = new Mission(
            name.Trim(),
            description.Trim(),
            ValueObjects.Difficulty.Create(difficulty),
            ValueObjects.MaximumTime.Create(maximumTimeMinutes),
            isActive: true,
            archivedAt: null,
            activationState: MissionActivation.Draft);

        mission.AddDomainEvent(new MissionCreatedEvent(mission));

        return mission;
    }

    public void UpdateDetails(string name, string description, string difficulty, int maximumTimeMinutes)
    {
        EnsureEditable();

        ValidateName(name);
        ValidateDescription(description);

        Name = name.Trim();
        Description = description.Trim();
        Difficulty = Difficulty.Create(difficulty);
        MaximumTime = MaximumTime.Create(maximumTimeMinutes);
        RepriceTargets();
        RefreshActivationState();

        AddDomainEvent(new MissionDetailsUpdatedEvent(this));
    }

    /// <summary>
    /// Terminal-retirement guard (HU-09): a deactivated mission is frozen and rejects every
    /// authoring mutation — details, structure, and targets alike — so it never drifts from the
    /// record the sessions sourced from it were built on. Application code that edits child nodes
    /// directly (rather than through an aggregate mutator) must call this before mutating.
    /// </summary>
    public void EnsureEditable()
    {
        if (!IsActive)
        {
            throw new MissionNotEditableWhileInactiveException();
        }
    }

    // ---- Composite authoring -------------------------------------------------

    public Stage AddStage(string title, int sequenceOrder)
    {
        EnsureEditable();

        var stage = Stage.Create(title, sequenceOrder);
        _stages.Add(stage);

        RecordStructureChange(new MissionNodeAddedEvent(this, stage));
        return stage;
    }

    public Substage AddSubstage(int stageId, Substage substage)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(substage);

        var stage = FindStage(stageId);
        stage.AddSubstage(substage);

        RecordStructureChange(new MissionNodeAddedEvent(this, substage));
        return substage;
    }

    public Clue AddClue(int stageId, int substageId, Clue clue)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(clue);

        var substage = FindSubstage(stageId, substageId);
        substage.AddClue(clue);

        RecordStructureChange(new MissionNodeAddedEvent(this, clue));
        return clue;
    }

    public void RenameNode(int stageId, string title, int sequenceOrder)
    {
        EnsureEditable();
        var stage = FindStage(stageId);
        stage.Rename(title, sequenceOrder);

        RecordStructureChange(new MissionNodeUpdatedEvent(this, stage));
    }

    public void RemoveStage(int stageId)
    {
        EnsureEditable();
        var stage = FindStage(stageId);
        _stages.Remove(stage);

        RecordStructureChange(new MissionNodeRemovedEvent(this, stage));
    }

    // ---- Treasure-hunt target authoring -------------------------------------

    public Target AddTarget(int stageId, int substageId, string name, string qrCode, int sequenceOrder, double latitude, double longitude, bool isActive = true)
    {
        EnsureEditable();
        EnsureQrCodeUniqueWithinMission(qrCode, null);
        var substage = FindSubstage(stageId, substageId);
        var target = substage.AddTarget(name, qrCode, sequenceOrder, DeriveTargetScore(), latitude, longitude, isActive);

        AddDomainEvent(new TargetAddedToSubstageEvent(this, substage, target));
        RefreshActivationState();
        return target;
    }

    public Target UpdateTarget(int stageId, int substageId, int targetId, string name, string qrCode, int sequenceOrder, double latitude, double longitude, bool isActive)
    {
        EnsureEditable();
        EnsureQrCodeUniqueWithinMission(qrCode, targetId);
        var substage = FindSubstage(stageId, substageId);
        var target = substage.UpdateTarget(targetId, name, qrCode, sequenceOrder, latitude, longitude, isActive, DeriveTargetScore());

        AddDomainEvent(new TargetUpdatedEvent(this, substage, target));
        RefreshActivationState();
        return target;
    }

    // QR uniqueness is mission-scoped, not substage-scoped: SessionOperations resolves a scan against
    // the whole mission snapshot, so two targets sharing a code anywhere in the mission make the scan
    // ambiguous at runtime. Only the aggregate root spans every stage/substage, so the invariant lives
    // here. <paramref name="excludedTargetId"/> lets an update keep its own current code.
    private void EnsureQrCodeUniqueWithinMission(string qrCode, int? excludedTargetId)
    {
        // A blank code is Target's own validation concern (TargetQrCodeRequiredException); do not
        // pre-empt it with a misleading uniqueness failure.
        if (string.IsNullOrWhiteSpace(qrCode))
        {
            return;
        }

        var normalized = qrCode.Trim();
        var clashes = _stages
            .SelectMany(stage => stage.Substages)
            .SelectMany(substage => substage.Targets)
            .Any(target =>
                target.Id != excludedTargetId &&
                string.Equals(target.QrCode, normalized, StringComparison.OrdinalIgnoreCase));

        if (clashes)
        {
            throw new TargetQrCodeMustBeUniqueWithinMissionException();
        }
    }

    // A target's score is not authored: it is fixed by the mission's difficulty
    // (base 50 * difficulty factor). Derived on add/update and re-derived for every
    // target when the mission's difficulty changes, so scores never drift from it.
    private int DeriveTargetScore() => ScoreValue.BaseTargetScore * Difficulty.ScoreFactor;

    private void RepriceTargets()
    {
        var score = DeriveTargetScore();

        foreach (var target in _stages.SelectMany(stage => stage.Substages).SelectMany(substage => substage.Targets))
        {
            target.Reprice(score);
        }
    }

    public void RemoveTarget(int stageId, int substageId, int targetId)
    {
        EnsureEditable();
        var substage = FindSubstage(stageId, substageId);
        substage.RemoveTarget(targetId);

        AddDomainEvent(new TargetRemovedFromSubstageEvent(this, substage, targetId));
        RefreshActivationState();
    }

    public void AssociateClueWithTarget(int stageId, int substageId, int targetId, Clue clue)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(clue);

        var substage = FindSubstage(stageId, substageId);
        var target = substage.AssociateClueWithTarget(targetId, clue);

        // Clue association is guidance only and never advances the target/substage,
        // so it deliberately does NOT refresh readiness.
        AddDomainEvent(new ClueAssociatedWithTargetEvent(this, substage, target, clue));
    }

    // ---- Trivia substage authoring ------------------------------------------

    public void SelectTriviaQuiz(int stageId, int substageId, int triviaQuizId)
    {
        EnsureEditable();
        var substage = FindSubstage(stageId, substageId);
        substage.SelectTriviaQuiz(triviaQuizId);

        RefreshActivationState();
    }

    public void RecordStructureChanged()
    {
        AddDomainEvent(new MissionStructureChangedEvent(this));
        RefreshActivationState();
    }

    // ---- Activation / readiness ---------------------------------------------

    public void Activate()
    {
        // Deactivation is terminal retirement (HU-09): a retired mission can never be brought back,
        // so it is not a candidate for activation. This must be checked before the readiness gate.
        if (!IsActive)
        {
            throw new MissionCannotBeReactivatedException();
        }

        if (ActivationState == MissionActivation.Ready)
        {
            throw new MissionAlreadyActiveException();
        }

        var failures = MissionActivationPolicy.EvaluateReadiness(this);

        if (failures.Count > 0)
        {
            throw new MissionNotReadyForActivationException(failures);
        }

        // An active mission is already un-archived — retirement is the only route to inactive and it
        // is terminal — so activation only needs to record readiness.
        ActivationState = MissionActivation.Ready;

        AddDomainEvent(new MissionActivatedEvent(this));
    }

    public void Deactivate(DateTimeOffset archivedAt)
    {
        if (!IsActive)
        {
            throw new MissionAlreadyDeactivatedException();
        }

        IsActive = false;
        ArchivedAt = archivedAt;
        ActivationState = MissionActivation.Inactive;

        AddDomainEvent(new MissionDeactivatedEvent(this));
    }

    private void RefreshActivationState()
    {
        // Authoring never auto-promotes a mission to Ready — that requires an explicit
        // Activate(). But if the mission was Ready and an authoring change broke the
        // runtime plan, demote it back to Draft so readiness stays truthful.
        if (!IsActive)
        {
            ActivationState = MissionActivation.Inactive;
            return;
        }

        if (ActivationState == MissionActivation.Ready
            && MissionActivationPolicy.SatisfiesRuntimePlan(this))
        {
            return;
        }

        ActivationState = MissionActivation.Draft;
    }

    private void RecordStructureChange(BaseEvent nodeEvent)
    {
        AddDomainEvent(nodeEvent);
        RecordStructureChanged();
    }

    private Stage FindStage(int stageId)
    {
        var stage = _stages.SingleOrDefault(existing => existing.Id == stageId);

        if (stage is null)
        {
            throw new MissionNodeNotFoundException(stageId);
        }

        return stage;
    }

    private Substage FindSubstage(int stageId, int substageId)
    {
        var stage = FindStage(stageId);
        var substage = stage.Substages.SingleOrDefault(existing => existing.Id == substageId);

        if (substage is null)
        {
            throw new MissionNodeNotFoundException(substageId);
        }

        return substage;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MissionNameRequiredException();
        }
    }

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new MissionDescriptionRequiredException();
        }
    }
}
