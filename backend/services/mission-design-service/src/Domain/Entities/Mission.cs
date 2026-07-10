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
        ValidateName(name);
        ValidateDescription(description);

        Name = name.Trim();
        Description = description.Trim();
        Difficulty = Difficulty.Create(difficulty);
        MaximumTime = MaximumTime.Create(maximumTimeMinutes);
        RefreshActivationState();

        AddDomainEvent(new MissionDetailsUpdatedEvent(this));
    }

    // ---- Composite authoring -------------------------------------------------

    public Stage AddStage(string title, int sequenceOrder)
    {
        var stage = Stage.Create(title, sequenceOrder);
        _stages.Add(stage);

        RecordStructureChange(new MissionNodeAddedEvent(this, stage));
        return stage;
    }

    public Substage AddSubstage(int stageId, Substage substage)
    {
        ArgumentNullException.ThrowIfNull(substage);

        var stage = FindStage(stageId);
        stage.AddSubstage(substage);

        RecordStructureChange(new MissionNodeAddedEvent(this, substage));
        return substage;
    }

    public Clue AddClue(int stageId, int substageId, Clue clue)
    {
        ArgumentNullException.ThrowIfNull(clue);

        var substage = FindSubstage(stageId, substageId);
        substage.AddClue(clue);

        RecordStructureChange(new MissionNodeAddedEvent(this, clue));
        return clue;
    }

    public void RenameNode(int stageId, string title, int sequenceOrder)
    {
        var stage = FindStage(stageId);
        stage.Rename(title, sequenceOrder);

        RecordStructureChange(new MissionNodeUpdatedEvent(this, stage));
    }

    public void RemoveStage(int stageId)
    {
        var stage = FindStage(stageId);
        _stages.Remove(stage);

        RecordStructureChange(new MissionNodeRemovedEvent(this, stage));
    }

    // ---- Treasure-hunt target authoring -------------------------------------

    public Target AddTarget(int stageId, int substageId, string name, string qrCode, int sequenceOrder, int score, bool isActive = true)
    {
        var substage = FindSubstage(stageId, substageId);
        var target = substage.AddTarget(name, qrCode, sequenceOrder, score, isActive);

        AddDomainEvent(new TargetAddedToSubstageEvent(this, substage, target));
        RefreshActivationState();
        return target;
    }

    public Target UpdateTarget(int stageId, int substageId, int targetId, string name, string qrCode, int sequenceOrder, bool isActive, int? score = null)
    {
        var substage = FindSubstage(stageId, substageId);
        var target = substage.UpdateTarget(targetId, name, qrCode, sequenceOrder, isActive, score);

        AddDomainEvent(new TargetUpdatedEvent(this, substage, target));
        RefreshActivationState();
        return target;
    }

    public void RemoveTarget(int stageId, int substageId, int targetId)
    {
        var substage = FindSubstage(stageId, substageId);
        substage.RemoveTarget(targetId);

        AddDomainEvent(new TargetRemovedFromSubstageEvent(this, substage, targetId));
        RefreshActivationState();
    }

    public void AssociateClueWithTarget(int stageId, int substageId, int targetId, Clue clue)
    {
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
        if (ActivationState == MissionActivation.Ready)
        {
            throw new MissionAlreadyActiveException();
        }

        var failures = MissionActivationPolicy.EvaluateReadiness(this);

        if (failures.Count > 0)
        {
            throw new MissionNotReadyForActivationException(failures);
        }

        IsActive = true;
        ArchivedAt = null;
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
