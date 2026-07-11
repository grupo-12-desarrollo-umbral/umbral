using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

// Pre-close restricted trivia monitor projection (HU-36A): the identity of the active synchronized
// question — (SubstageSnapshotId, QuestionSequenceOrder), since a snapshotted question carries no Guid —
// plus the ordered per-team answered/not-answered roster. Like its cells it exposes no
// option/correctness/score, so nothing before close can reveal the option a team chose.
public sealed class TriviaAnsweredMonitorSnapshot : ValueObject
{
    private readonly List<TeamAnsweredStatus> _teamStatuses;

    private TriviaAnsweredMonitorSnapshot(Guid substageSnapshotId, int questionSequenceOrder, IEnumerable<TeamAnsweredStatus> teamStatuses)
    {
        if (substageSnapshotId == Guid.Empty)
        {
            throw new SubstageSnapshotIdRequiredException();
        }

        SubstageSnapshotId = substageSnapshotId;
        QuestionSequenceOrder = questionSequenceOrder;
        _teamStatuses = teamStatuses?.ToList() ?? [];
    }

    // Active-question identity — the (substage snapshot, sequence order) pair that keys a snapshotted
    // trivia question.
    public Guid SubstageSnapshotId { get; }

    public int QuestionSequenceOrder { get; }

    public IReadOnlyList<TeamAnsweredStatus> TeamStatuses => _teamStatuses.AsReadOnly();

    public static TriviaAnsweredMonitorSnapshot Create(
        Guid substageSnapshotId,
        int questionSequenceOrder,
        IEnumerable<TeamAnsweredStatus> teamStatuses)
    {
        return new TriviaAnsweredMonitorSnapshot(substageSnapshotId, questionSequenceOrder, teamStatuses);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SubstageSnapshotId;
        yield return QuestionSequenceOrder;

        foreach (var status in _teamStatuses)
        {
            yield return status;
        }
    }
}
