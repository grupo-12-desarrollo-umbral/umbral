using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class Mission : BaseAuditableEntity
{
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
            activationState: MissionActivationPolicy.DetermineActivationState(
                isActive: true,
                satisfiesStructureRequirements: false));

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
        ActivationState = MissionActivationPolicy.DetermineActivationState(
            IsActive,
            satisfiesStructureRequirements: false);

        AddDomainEvent(new MissionDetailsUpdatedEvent(this));
    }

    public void Deactivate(DateTimeOffset archivedAt)
    {
        if (!IsActive)
        {
            throw new MissionAlreadyDeactivatedException();
        }

        IsActive = false;
        ArchivedAt = archivedAt;
        ActivationState = MissionActivationPolicy.DetermineActivationState(
            isActive: false,
            satisfiesStructureRequirements: false);

        AddDomainEvent(new MissionDeactivatedEvent(this));
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
