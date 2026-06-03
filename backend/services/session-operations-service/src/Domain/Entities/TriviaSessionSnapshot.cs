using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class TriviaSessionSnapshot : BaseEntity
{
    private readonly List<TriviaQuestionSnapshot> _questions = [];

    private TriviaSessionSnapshot()
    {
        QuizTitle = string.Empty;
    }

    private TriviaSessionSnapshot(string quizTitle, IEnumerable<TriviaQuestionSnapshot> questions)
    {
        var normalizedQuestions = questions?.ToArray() ?? [];
        if (normalizedQuestions.Length == 0)
        {
            throw new TriviaSessionSnapshotMustContainQuestionsException();
        }

        QuizTitle = quizTitle.Trim();
        _questions.AddRange(normalizedQuestions);
    }

    public string QuizTitle { get; private set; }

    public IReadOnlyCollection<TriviaQuestionSnapshot> Questions => _questions.AsReadOnly();

    public static TriviaSessionSnapshot Create(string quizTitle, IEnumerable<TriviaQuestionSnapshot> questions)
    {
        return new TriviaSessionSnapshot(quizTitle, questions);
    }
}
