using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// A nested mission node under a <see cref="Stage"/>. Each substage has exactly one
/// <see cref="SubstagePlayMode"/>.
/// <list type="bullet">
/// <item><c>TreasureHunt</c>: owns ordered <see cref="Target"/>s (target-based
/// progression) plus a winner <see cref="ScoreValue"/>.</item>
/// <item><c>Trivia</c>: references one published <c>TriviaQuiz</c> by identity. The
/// whole quiz is selected; <c>TriviaQuiz</c> is reusable authoring content, not
/// runtime session source content.</item>
/// </list>
/// A substage may own optional <see cref="Clue"/> children in either mode.
/// </summary>
public sealed class Substage : MissionNode
{
    private readonly List<Target> _targets = [];

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

    public ScoreValue? WinnerScore { get; private set; }

    // Trivia content: identity of the single published TriviaQuiz selected in full.
    public int? TriviaQuizId { get; private set; }

    public IEnumerable<Clue> Clues => Children.OfType<Clue>();

    public static Substage CreateTreasureHunt(string title, int sequenceOrder)
    {
        return new Substage(title, sequenceOrder, SubstagePlayMode.TreasureHunt);
    }

    public static Substage CreateTrivia(string title, int sequenceOrder)
    {
        return new Substage(title, sequenceOrder, SubstagePlayMode.Trivia);
    }

    public Target AddTarget(string name, string qrCode, int sequenceOrder, bool isActive = true)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);

        var target = Target.Create(name, qrCode, sequenceOrder, isActive);
        _targets.Add(target);
        return target;
    }

    public Target UpdateTarget(int targetId, string name, string qrCode, int sequenceOrder, bool isActive)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);

        var target = FindTarget(targetId);
        target.UpdateDetails(name, qrCode, sequenceOrder, isActive);
        return target;
    }

    public void RemoveTarget(int targetId)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);

        var target = FindTarget(targetId);
        _targets.Remove(target);
    }

    public void SetWinnerScore(int points)
    {
        EnsurePlayMode(SubstagePlayMode.TreasureHunt);
        WinnerScore = ScoreValue.Create(points);
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
