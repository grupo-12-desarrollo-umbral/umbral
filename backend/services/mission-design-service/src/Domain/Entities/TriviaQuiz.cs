using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Domain.Entities;

public sealed class TriviaQuiz : BaseAuditableEntity
{
    private readonly List<TriviaQuestion> _questions = [];

    private static readonly TriviaQuizAuthoringTemplate CreateTemplate = new CreateTriviaQuizAuthoringTemplate();
    private static readonly TriviaQuizAuthoringTemplate UpdateTemplate = new UpdateTriviaQuizAuthoringTemplate();
    private static readonly TriviaQuestionAuthoringTemplate AddQuestionTemplate = new AddTriviaQuestionAuthoringTemplate();
    private static readonly TriviaQuestionAuthoringTemplate UpdateQuestionTemplate = new UpdateTriviaQuestionAuthoringTemplate();
    private static readonly TriviaQuizLifecycleTemplate PublishTemplate = new PublishTriviaQuizLifecycleTemplate();
    private static readonly TriviaQuizLifecycleTemplate ArchiveTemplate = new ArchiveTriviaQuizLifecycleTemplate();

    private TriviaQuiz()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    private TriviaQuiz(
        string title,
        string description,
        TriviaQuizStatus status,
        DateTimeOffset? publishedAt,
        IEnumerable<TriviaQuestion> questions)
    {
        Title = title;
        Description = description;
        Status = status;
        PublishedAt = publishedAt;
        _questions.AddRange(questions);
    }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public TriviaQuizStatus Status { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public bool IsSourceReady => Status == TriviaQuizStatus.Published;

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
            publishedAt: null,
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

    public void Publish(DateTimeOffset publishedAt)
    {
        PublishTemplate.Apply(this, publishedAt);
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        ArchiveTemplate.Apply(this, archivedAt);
    }

    public void MarkAsPublished()
    {
        Publish(DateTimeOffset.UtcNow);
    }

    public void MarkAsArchived()
    {
        Archive(DateTimeOffset.UtcNow);
    }

    public TriviaQuestion AddQuestion(
        string prompt,
        int sequenceOrder,
        int scoreValue,
        int timeLimitSeconds,
        string? explanation,
        IEnumerable<TriviaOption> options,
        bool isActive = true)
    {
        var question = AddQuestionTemplate.Apply(
            this,
            new TriviaQuestionAuthoringDraft(
                null,
                prompt,
                sequenceOrder,
                scoreValue,
                timeLimitSeconds,
                explanation,
                options,
                isActive));

        _questions.Add(question);
        AddDomainEvent(new TriviaQuestionAddedEvent(this, question));

        return question;
    }

    public TriviaQuestion UpdateQuestion(
        int questionId,
        string prompt,
        int sequenceOrder,
        int scoreValue,
        int timeLimitSeconds,
        string? explanation,
        IEnumerable<TriviaOption> options,
        bool isActive = true)
    {
        var question = UpdateQuestionTemplate.Apply(
            this,
            new TriviaQuestionAuthoringDraft(
                questionId,
                prompt,
                sequenceOrder,
                scoreValue,
                timeLimitSeconds,
                explanation,
                options,
                isActive));

        AddDomainEvent(new TriviaQuestionUpdatedEvent(this, question));

        return question;
    }

    private void ReplaceQuestions(IEnumerable<TriviaQuestion> questions)
    {
        _questions.Clear();
        _questions.AddRange(questions);
    }

    private abstract class TriviaQuizLifecycleTemplate
    {
        public void Apply(TriviaQuiz quiz, DateTimeOffset transitionedAt)
        {
            EnsureCurrentStateAllowsTransition(quiz.Status);
            EnsureReadiness(quiz);
            ApplyTransition(quiz, transitionedAt);
            RaiseDomainEvent(quiz);
        }

        protected abstract void EnsureCurrentStateAllowsTransition(TriviaQuizStatus status);

        protected virtual void EnsureReadiness(TriviaQuiz quiz)
        {
        }

        protected abstract void ApplyTransition(TriviaQuiz quiz, DateTimeOffset transitionedAt);

        protected abstract void RaiseDomainEvent(TriviaQuiz quiz);
    }

    private sealed record ValidatedTriviaQuizAuthoring(string Title, string Description, IReadOnlyCollection<TriviaQuestion> Questions);
    private sealed record TriviaQuestionAuthoringDraft(
        int? QuestionId,
        string Prompt,
        int SequenceOrder,
        int ScoreValue,
        int TimeLimitSeconds,
        string? Explanation,
        IEnumerable<TriviaOption> Options,
        bool IsActive);

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

    private abstract class TriviaQuestionAuthoringTemplate
    {
        public TriviaQuestion Apply(TriviaQuiz quiz, TriviaQuestionAuthoringDraft draft)
        {
            EnsureEditable(quiz.Status);

            var targetQuestion = ResolveTargetQuestion(quiz, draft);
            var normalizedOptions = NormalizeOptions(draft.Options);

            EnsureQuestionSequenceIsUnique(quiz, draft.SequenceOrder, targetQuestion?.Id);
            EnsureOptionCount(normalizedOptions.Count);
            EnsureExactlyOneCorrectOption(normalizedOptions);
            EnsureDistinctOptionSequenceOrders(normalizedOptions);

            return BuildQuestion(targetQuestion, draft, normalizedOptions);
        }

        protected virtual void EnsureEditable(TriviaQuizStatus status)
        {
            if (status != TriviaQuizStatus.Draft)
            {
                throw new TriviaQuizNotEditableException(status);
            }
        }

        protected abstract TriviaQuestion? ResolveTargetQuestion(TriviaQuiz quiz, TriviaQuestionAuthoringDraft draft);

        protected abstract TriviaQuestion BuildQuestion(
            TriviaQuestion? targetQuestion,
            TriviaQuestionAuthoringDraft draft,
            IReadOnlyCollection<TriviaOption> normalizedOptions);

        private static IReadOnlyCollection<TriviaOption> NormalizeOptions(IEnumerable<TriviaOption> options)
        {
            return options.ToArray();
        }

        private static void EnsureQuestionSequenceIsUnique(TriviaQuiz quiz, int sequenceOrder, int? currentQuestionId)
        {
            if (quiz._questions.Any(question =>
                    question.SequenceOrder == sequenceOrder &&
                    question.Id != currentQuestionId))
            {
                throw new TriviaQuestionSequenceOrderMustBeUniqueException(sequenceOrder);
            }
        }

        private static void EnsureOptionCount(int count)
        {
            if (count is < 2 or > 4)
            {
                throw new TriviaQuestionMustHaveBetweenTwoAndFourOptionsException();
            }
        }

        private static void EnsureExactlyOneCorrectOption(IEnumerable<TriviaOption> options)
        {
            if (options.Count(option => option.IsCorrect) != 1)
            {
                throw new TriviaQuestionMustHaveExactlyOneCorrectOptionException();
            }
        }

        private static void EnsureDistinctOptionSequenceOrders(IEnumerable<TriviaOption> options)
        {
            var sequenceOrders = options.Select(option => option.SequenceOrder).ToArray();

            if (sequenceOrders.Length != sequenceOrders.Distinct().Count())
            {
                throw new TriviaOptionSequenceOrderMustBeUniqueException();
            }
        }
    }

    private sealed class AddTriviaQuestionAuthoringTemplate : TriviaQuestionAuthoringTemplate
    {
        protected override TriviaQuestion? ResolveTargetQuestion(TriviaQuiz quiz, TriviaQuestionAuthoringDraft draft)
        {
            return null;
        }

        protected override TriviaQuestion BuildQuestion(
            TriviaQuestion? targetQuestion,
            TriviaQuestionAuthoringDraft draft,
            IReadOnlyCollection<TriviaOption> normalizedOptions)
        {
            return TriviaQuestion.Create(
                draft.Prompt,
                draft.SequenceOrder,
                draft.ScoreValue,
                draft.TimeLimitSeconds,
                draft.Explanation,
                normalizedOptions,
                draft.IsActive);
        }
    }

    private sealed class UpdateTriviaQuestionAuthoringTemplate : TriviaQuestionAuthoringTemplate
    {
        protected override TriviaQuestion? ResolveTargetQuestion(TriviaQuiz quiz, TriviaQuestionAuthoringDraft draft)
        {
            var question = quiz._questions.SingleOrDefault(existing => existing.Id == draft.QuestionId);

            if (question is null)
            {
                throw new TriviaQuestionNotFoundException(draft.QuestionId!.Value);
            }

            return question;
        }

        protected override TriviaQuestion BuildQuestion(
            TriviaQuestion? targetQuestion,
            TriviaQuestionAuthoringDraft draft,
            IReadOnlyCollection<TriviaOption> normalizedOptions)
        {
            targetQuestion!.ApplyAuthoring(
                draft.Prompt,
                draft.SequenceOrder,
                draft.ScoreValue,
                draft.TimeLimitSeconds,
                draft.Explanation,
                normalizedOptions,
                draft.IsActive);

            return targetQuestion;
        }
    }

    private sealed class PublishTriviaQuizLifecycleTemplate : TriviaQuizLifecycleTemplate
    {
        protected override void EnsureCurrentStateAllowsTransition(TriviaQuizStatus status)
        {
            if (status != TriviaQuizStatus.Draft)
            {
                throw new TriviaQuizCannotBePublishedInCurrentStateException(status);
            }
        }

        protected override void EnsureReadiness(TriviaQuiz quiz)
        {
            TriviaPublicationPolicy.EnsurePublishable(quiz);
        }

        protected override void ApplyTransition(TriviaQuiz quiz, DateTimeOffset transitionedAt)
        {
            quiz.Status = TriviaQuizStatus.Published;
            quiz.PublishedAt = transitionedAt;
        }

        protected override void RaiseDomainEvent(TriviaQuiz quiz)
        {
            quiz.AddDomainEvent(new TriviaQuizPublishedEvent(quiz));
        }
    }

    private sealed class ArchiveTriviaQuizLifecycleTemplate : TriviaQuizLifecycleTemplate
    {
        protected override void EnsureCurrentStateAllowsTransition(TriviaQuizStatus status)
        {
            if (status == TriviaQuizStatus.Archived)
            {
                throw new TriviaQuizCannotBeArchivedInCurrentStateException(status);
            }
        }

        protected override void ApplyTransition(TriviaQuiz quiz, DateTimeOffset transitionedAt)
        {
            quiz.Status = TriviaQuizStatus.Archived;
        }

        protected override void RaiseDomainEvent(TriviaQuiz quiz)
        {
            quiz.AddDomainEvent(new TriviaQuizArchivedEvent(quiz));
        }
    }
}
