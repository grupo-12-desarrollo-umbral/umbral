using System.Reflection;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// HU-16 (DES-75): whole-quiz snapshot-content-fidelity lock. HU-15 already ships the copy;
// these tests freeze the invariant that the ENTIRE published quiz is snapshotted faithfully,
// in authored order, and that no partial-selection concept exists anywhere in the domain.
public sealed class MissionRuntimeSnapshotTriviaFidelityTests
{
    [Fact]
    public void Create_CopiesEveryQuestionAndOption_WithFullFieldFidelity()
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var sourceQuestions = BuildVariedQuiz(triviaSubstage.SubstageSnapshotId);

        var snapshot = BuildTriviaSnapshot(triviaSubstage, sourceQuestions);

        // Whole quiz: count parity, no dropped questions.
        snapshot.TriviaQuestionSnapshots.Should().HaveCount(sourceQuestions.Count);

        // Strict authored order + per-field content parity, including options and the correct flag.
        var frozen = snapshot.TriviaQuestionSnapshots.OrderBy(q => q.SequenceOrder).ToArray();
        var source = sourceQuestions.OrderBy(q => q.SequenceOrder).ToArray();

        for (var i = 0; i < source.Length; i++)
        {
            frozen[i].Prompt.Should().Be(source[i].Prompt);
            frozen[i].SequenceOrder.Should().Be(source[i].SequenceOrder);
            frozen[i].ScoreValue.Should().Be(source[i].ScoreValue);
            frozen[i].TimeLimitSeconds.Should().Be(source[i].TimeLimitSeconds);
            frozen[i].Explanation.Should().Be(source[i].Explanation);

            var frozenOptions = frozen[i].Options.OrderBy(o => o.SequenceOrder).ToArray();
            var sourceOptions = source[i].Options.OrderBy(o => o.SequenceOrder).ToArray();
            frozenOptions.Should().HaveCount(sourceOptions.Length);
            for (var j = 0; j < sourceOptions.Length; j++)
            {
                frozenOptions[j].OptionText.Should().Be(sourceOptions[j].OptionText);
                frozenOptions[j].SequenceOrder.Should().Be(sourceOptions[j].SequenceOrder);
                frozenOptions[j].IsCorrect.Should().Be(sourceOptions[j].IsCorrect);
            }

            // Exactly one correct option is preserved per question.
            frozen[i].Options.Count(o => o.IsCorrect).Should().Be(1);
        }
    }

    [Fact]
    public void Create_PreservesAuthoredInputOrder_NoReordering()
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        // Authored order is 1,2,3; feed them shuffled to prove the snapshot preserves the input sequence.
        var authored = BuildVariedQuiz(triviaSubstage.SubstageSnapshotId);
        var shuffled = new[] { authored[2], authored[0], authored[1] };

        var snapshot = BuildTriviaSnapshot(triviaSubstage, shuffled);

        snapshot.TriviaQuestionSnapshots.Select(q => q.SequenceOrder)
            .Should().Equal(shuffled.Select(q => q.SequenceOrder));
    }

    [Fact]
    public void Create_TriviaSubstageWithZeroQuestions_IsRejected()
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [triviaSubstage]);

        var act = () => MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Runtime Plan",
            MaximumTime.Create(30),
            [stage],
            [],
            []);

        act.Should().Throw<TriviaSubstageSnapshotMustContainQuestionsException>();
    }

    [Fact]
    public void Snapshot_IsImmutableAfterLiveSessionCreate()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaWithThreeQuestions();
        var frozen = session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Select(q => q.Prompt)
            .ToArray();

        // The read seam is a read-only collection: no Add/Remove surface to mutate post-creation.
        session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Should().BeAssignableTo<IReadOnlyCollection<TriviaQuestionSnapshot>>();

        // No public setter on any declared MissionRuntimeSnapshot state property -> immutable after Create.
        var mutableProps = typeof(MissionRuntimeSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();
        mutableProps.Should().BeEmpty();

        // Content is unchanged / observable identically.
        session.MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Select(q => q.Prompt).Should().Equal(frozen);
    }

    [Fact]
    public void Domain_HasNoPartialQuestionSelectionType()
    {
        var domainTypeNames = typeof(MissionRuntimeSnapshot).Assembly
            .GetTypes()
            .Select(t => t.Name)
            .ToArray();

        domainTypeNames.Should().NotContain("TriviaQuestionSelection");
        domainTypeNames.Should().NotContain(name =>
            name.Contains("QuestionSelection", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("PartialSelection", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("QuestionSubset", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<TriviaQuestionSnapshot> BuildVariedQuiz(Guid substageId)
    {
        return
        [
            TriviaQuestionSnapshot.Create(substageId, "Closest planet to the Sun?", 1, 1, 5, "Mercury.",
            [
                TriviaOptionSnapshot.Create("Mercury", 1, true),
                TriviaOptionSnapshot.Create("Venus", 2, false)
            ]),
            TriviaQuestionSnapshot.Create(substageId, "Largest ocean?", 2, 50, 60, null,
            [
                TriviaOptionSnapshot.Create("Atlantic", 1, false),
                TriviaOptionSnapshot.Create("Pacific", 2, true),
                TriviaOptionSnapshot.Create("Indian", 3, false)
            ]),
            TriviaQuestionSnapshot.Create(substageId, "Speed of light unit?", 3, 100, 120, "km/s.",
            [
                TriviaOptionSnapshot.Create("m/s", 1, false),
                TriviaOptionSnapshot.Create("km/s", 2, true),
                TriviaOptionSnapshot.Create("mph", 3, false),
                TriviaOptionSnapshot.Create("kn", 4, false)
            ])
        ];
    }

    private static MissionRuntimeSnapshot BuildTriviaSnapshot(
        SubstageSnapshot triviaSubstage,
        IReadOnlyList<TriviaQuestionSnapshot> questions)
    {
        var stage = StageSnapshot.Create("Stage One", 1, [triviaSubstage]);

        return MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Foundations of Science",
            MaximumTime.Create(30),
            [stage],
            [],
            questions);
    }
}
