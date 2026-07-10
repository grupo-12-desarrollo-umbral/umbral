using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// A nested mission node under a <see cref="Stage"/>. Each substage has exactly one
/// <see cref="SubstagePlayMode"/>.
/// <list type="bullet">
/// <item><c>TreasureHunt</c>: owns ordered <see cref="Target"/>s (target-based
/// progression), each carrying its own score.</item>
/// <item><c>Trivia</c>: references one published <c>TriviaQuiz</c> by identity. The
/// whole quiz is selected; <c>TriviaQuiz</c> is reusable authoring content, not
/// runtime session source content.</item>
/// </list>
/// A substage may own optional <see cref="Clue"/> children in either mode.
/// </summary>
public sealed class Substage : MissionNode
{
    private readonly List<Target> _targets = [];
    private readonly List<Clue> _clues = [];

    private Substage()
    {
    }

    private Substage(string title, int sequenceOrder, SubstagePlayMode playMode)
        : base(title, sequenceOrder)
    {
        PlayMode = playMode;
    }

    public override MissionNodeType NodeType => MissionNodeType.Substage;

    public SubstagePlayMode PlayMode { get; private set; }

    // TreasureHunt content.
    public IReadOnlyList<Target> Targets =>
        _targets.OrderBy(target => target.SequenceOrder).ToList().AsReadOnly();

    // Trivia content: identity of the single published TriviaQuiz selected in full.
    public int? TriviaQuizId { get; private set; }

    public IEnumerable<Clue> Clues =>
        _clues.OrderBy(clue => clue.SequenceOrder).ToList().AsReadOnly();

    public static Substage CreateTreasureHunt(string title, int sequenceOrder)
    {
        return new Substage(title, sequenceOrder, SubstagePlayMode.TreasureHunt);
    }

    public static Substage CreateTrivia(string title, int sequenceOrder)
    {
        return new Substage(title, sequenceOrder, SubstagePlayMode.Trivia);
    }

    public Target AddTarget(string name, string qrCode, int sequenceOrder, int score, bool isActive = true)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);

        var target = Target.Create(name, qrCode, sequenceOrder, score, isActive);
        _targets.Add(target);
        return target;
    }

    public Target UpdateTarget(int targetId, string name, string qrCode, int sequenceOrder, bool isActive, int? score = null)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);

        var target = FindTarget(targetId);
        target.UpdateDetails(name, qrCode, sequenceOrder, isActive, score);
        return target;
    }

    public void RemoveTarget(int targetId)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);

        var target = FindTarget(targetId);
        _targets.Remove(target);
    }

    public Clue AddClue(Clue clue)
    {
        AddChild(clue);
        return clue;
    }

    public Target AssociateClueWithTarget(int targetId, Clue clue)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);
        ArgumentNullException.ThrowIfNull(clue);

        var target = FindTarget(targetId);

        if (!Children.Contains(clue))
        {
            throw new ClueMustBelongToSameSubstageException();
        }

        target.AssociateClue(clue.Id);
        return target;
    }

    public void SelectTriviaQuiz(int triviaQuizId)
    {
        EnsurePlayMode(SubstagePlayMode.Trivia);

        if (triviaQuizId <= 0)
        {
            throw new SubstageRequiresPlayModeException();
        }

        TriviaQuizId = triviaQuizId;
    }

    protected override IEnumerable<MissionNode> ChildNodes => _clues;

    protected override void AddChildNode(MissionNode child)
    {
        _clues.Add((Clue)child);
    }

    protected override void RemoveChildNode(MissionNode child)
    {
        if (child is Clue clue)
        {
            _clues.Remove(clue);
        }
    }

    protected override bool CanContain(MissionNodeType childType)
    {
        return childType == MissionNodeType.Clue;
    }

    private Target FindTarget(int targetId)
    {
        var target = _targets.SingleOrDefault(existing => existing.Id == targetId);

        if (target is null)
        {
            throw new TargetNotFoundException(targetId);
        }

        return target;
    }

    private void EnsurePlayMode(SubstagePlayMode expected)
    {
        if (PlayMode != expected)
        {
            throw new SubstagePlayModeMismatchException(expected, PlayMode);
        }
    }
}
