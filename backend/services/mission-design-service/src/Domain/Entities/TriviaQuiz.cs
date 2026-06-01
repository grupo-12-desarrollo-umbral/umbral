using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class TriviaQuiz : BaseAuditableEntity
{
    private readonly List<TriviaQuestion> _questions = [];

    private static readonly TriviaQuizAuthoringTemplate CreateTemplate = new CreateTriviaQuizAuthoringTemplate();
    private static readonly TriviaQuizAuthoringTemplate UpdateTemplate = new UpdateTriviaQuizAuthoringTemplate();

    private TriviaQuiz()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    private TriviaQuiz(
        string title,
        string description,
        TriviaQuizStatus status,
        IEnumerable<TriviaQuestion> questions)
    {
        Title = title;
        Description = description;
        Status = status;
        _questions.AddRange(questions);
    }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public TriviaQuizStatus Status { get; private set; }

    public IReadOnlyCollection<TriviaQuestion> Questions => _questions.AsReadOnly();

    public static TriviaQuiz Create(
        string title,
        string description,
        IEnumerable<TriviaQuestion>? questions = null)
    {
        var validated = CreateTemplate.Validate(title, description, TriviaQuizStatus.Draft, questions);

        var quiz = new TriviaQuiz(
            validated.Title,
            validated.Description,
            TriviaQuizStatus.Draft,
            validated.Questions);

        quiz.AddDomainEvent(new TriviaQuizCreatedEvent(quiz));

        return quiz;
    }

    public void UpdateDetails(
        string title,
        string description,
        IEnumerable<TriviaQuestion>? questions = null)
    {
        var validated = UpdateTemplate.Validate(title, description, Status, questions ?? _questions);

        Title = validated.Title;
        Description = validated.Description;
        ReplaceQuestions(validated.Questions);

        AddDomainEvent(new TriviaQuizDetailsUpdatedEvent(this));
    }

    public void MarkAsPublished()
    {
        Status = TriviaQuizStatus.Published;
    }

    public void MarkAsArchived()
    {
        Status = TriviaQuizStatus.Archived;
    }

    private void ReplaceQuestions(IEnumerable<TriviaQuestion> questions)
    {
        _questions.Clear();
        _questions.AddRange(questions);
    }

    private sealed record ValidatedTriviaQuizAuthoring(string Title, string Description, IReadOnlyCollection<TriviaQuestion> Questions);

    private abstract class TriviaQuizAuthoringTemplate
    {
        public ValidatedTriviaQuizAuthoring Validate(
            string title,
            string description,
            TriviaQuizStatus status,
            IEnumerable<TriviaQuestion>? questions)
        {
            ValidateTitle(title);
            ValidateDescription(description);
            EnsureEditable(status);
            return new ValidatedTriviaQuizAuthoring(
                title.Trim(),
                description.Trim(),
                NormalizeQuestions(questions));
        }

        protected virtual void EnsureEditable(TriviaQuizStatus status)
        {
        }

        private static void ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new TriviaQuizTitleRequiredException();
            }
        }

        private static void ValidateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new TriviaQuizDescriptionRequiredException();
            }
        }

        private static IReadOnlyCollection<TriviaQuestion> NormalizeQuestions(IEnumerable<TriviaQuestion>? questions)
        {
            return questions?.ToArray() ?? [];
        }
    }

    private sealed class CreateTriviaQuizAuthoringTemplate : TriviaQuizAuthoringTemplate
    {
    }

    private sealed class UpdateTriviaQuizAuthoringTemplate : TriviaQuizAuthoringTemplate
    {
        protected override void EnsureEditable(TriviaQuizStatus status)
        {
            if (status != TriviaQuizStatus.Draft)
            {
                throw new TriviaQuizNotEditableException(status);
            }
        }
    }
}
