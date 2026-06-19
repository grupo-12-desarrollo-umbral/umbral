using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class InvalidMissionNodeChildException : Exception
{
    public InvalidMissionNodeChildException(MissionNodeType parent, MissionNodeType child)
        : base($"A {child} node cannot be placed directly under a {parent} node.")
    {
        Parent = parent;
        Child = child;
    }

    public MissionNodeType Parent { get; }

    public MissionNodeType Child { get; }
}
