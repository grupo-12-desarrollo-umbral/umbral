using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
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

    private Mission(string name, string description, Difficulty difficulty, MaximumTime maximumTime)
    {
        Name = name;
        Description = description;
        Difficulty = difficulty;
        MaximumTime = maximumTime;
        ActivationState = MissionActivation.Draft;
    }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public Difficulty Difficulty { get; private set; }

    public MaximumTime MaximumTime { get; private set; }

    public MissionActivation ActivationState { get; private set; }

    public static Mission Create(string name, string description, string difficulty, int maximumTimeMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MissionNameRequiredException();
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new MissionDescriptionRequiredException();
        }

        var mission = new Mission(
            name.Trim(),
            description.Trim(),
            ValueObjects.Difficulty.Create(difficulty),
            ValueObjects.MaximumTime.Create(maximumTimeMinutes));

        mission.AddDomainEvent(new MissionCreatedEvent(mission));

        return mission;
    }
}
