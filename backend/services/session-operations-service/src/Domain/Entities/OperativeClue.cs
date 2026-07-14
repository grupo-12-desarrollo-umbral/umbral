namespace umbral_backend.Domain.Entities;

public sealed class OperativeClue : BaseEntity
{
    private OperativeClue()
    {
        OperativeClueId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamId = Guid.Empty;
        ClueText = string.Empty;
    }

    private OperativeClue(
        Guid operativeClueId,
        Guid liveSessionId,
        Guid teamId,
        string clueText,
        int createdByUserId,
        DateTimeOffset createdAt)
    {
        OperativeClueId = operativeClueId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        ClueText = clueText;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid OperativeClueId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public string ClueText { get; private set; }

    public int CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static OperativeClue Create(
        Guid liveSessionId,
        Guid teamId,
        string clueText,
        int createdByUserId,
        DateTimeOffset createdAt)
    {
        return new OperativeClue(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            clueText,
            createdByUserId,
            createdAt);
    }
}
