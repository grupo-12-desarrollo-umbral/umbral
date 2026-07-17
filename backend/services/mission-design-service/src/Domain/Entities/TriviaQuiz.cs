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
    private static readonly TriviaQuestionRemovalTemplate RemoveQuestionTemplate = new RemoveTriviaQuestionAuthoringTemplate();
    private static readonly TriviaQuizLifecycleTemplate PublishTemplate = new PublishTriviaQuizLifecycleTemplate();
    private static readonly TriviaQuizLifecycleTemplate ArchiveTemplate = new ArchiveTriviaQuizLifecycleTemplate();
    private static readonly TriviaQuizReuseWorkflowTemplate<TriviaQuiz> DuplicateWorkflow = new DuplicateTriviaQuizWorkflowTemplate();
    private static readonly TriviaQuizReuseWorkflowTemplate<TriviaQuiz> RetireWorkflow = new RetireTriviaQuizWorkflowTemplate();
    private static readonly TriviaQuizReuseWorkflowTemplate<TriviaQuiz> DestructiveRemovalGuardWorkflow = new DestructiveRemovalTriviaQuizWorkflowTemplate();

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
        IEnumerable<TriviaQuestion> questions,
        int? sourceTriviaQuizId,
        bool hasUsageHistory)
    {
        Title = title;
        Description = description;
        Status = status;
        PublishedAt = publishedAt;
        SourceTriviaQuizId = sourceTriviaQuizId;
        HasUsageHistory = hasUsageHistory;
        _questions.AddRange(questions);
    }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public TriviaQuizStatus Status { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public int? SourceTriviaQuizId { get; private set; }

    public bool HasUsageHistory { get; private set; }

    public bool IsDuplicate => SourceTriviaQuizId.HasValue;

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
            validated.Questions,
            sourceTriviaQuizId: null,
            hasUsageHistory: false);

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

    public TriviaQuiz Duplicate()
    {
        return DuplicateWorkflow.Apply(this, TriviaQuizReuseContext.Empty);
    }

    public void RetireFromFutureUse(DateTimeOffset archivedAt)
    {
        RetireWorkflow.Apply(this, new TriviaQuizReuseContext(archivedAt));
    }

    public void EnsureCanBeDestructivelyRemoved()
    {
        DestructiveRemovalGuardWorkflow.Apply(this, TriviaQuizReuseContext.Empty);
    }

    public void MarkAsUsedInSession()
    {
        HasUsageHistory = true;
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
                scoreValue,
                timeLimitSeconds,
                explanation,
                options,
                isActive));

        AddDomainEvent(new TriviaQuestionUpdatedEvent(this, question));

        return question;
    }

    public TriviaQuestion RemoveQuestion(int triviaQuestionId)
    {
        var removedQuestion = RemoveQuestionTemplate.Apply(this, triviaQuestionId);

        AddDomainEvent(new TriviaQuestionRemovedEvent(this, removedQuestion));

        return removedQuestion;
    }

    private void ReplaceQuestions(IEnumerable<TriviaQuestion> questions)
    {
        _questions.Clear();
        _questions.AddRange(questions);
    }

    private static TriviaQuestion CloneQuestion(TriviaQuestion sourceQuestion)
    {
        return TriviaQuestion.Create(
            sourceQuestion.Prompt,
            sourceQuestion.ScoreValue,
            sourceQuestion.TimeLimit?.Seconds,
            sourceQuestion.Explanation,
            sourceQuestion.Options.Select(CloneOption).ToArray(),
            sourceQuestion.IsActive);
    }

    private static TriviaOption CloneOption(TriviaOption sourceOption)
    {
        return TriviaOption.Create(
            sourceOption.OptionText,
            sourceOption.SequenceOrder,
            sourceOption.IsCorrect);
    }

    private static int? ResolveDuplicateLineageSourceId(TriviaQuiz sourceQuiz)
    {
        return sourceQuiz.Id > 0
            ? sourceQuiz.Id
            : sourceQuiz.SourceTriviaQuizId;
    }

    // Single source of truth for the authoring editability rule: quiz details,
    // question add/update, and question removal are only allowed while the quiz is a
    // Draft. Shared so those authoring paths cannot drift apart.
    private static void EnsureQuizEditable(TriviaQuizStatus status)
    {
        if (status != TriviaQuizStatus.Draft)
        {
            throw new TriviaQuizNotEditableException(status);
        }
    }

    // Single source of truth for the per-question authoring rules (option count and
    // exactly-one-correct). Shared so the quiz-authoring path (create/update quiz) and the
    // single-question authoring path (add/update question) enforce identical invariants and
    // cannot drift apart.
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

    private sealed record TriviaQuizReuseContext(DateTimeOffset? ArchivedAt)
    {
        public static TriviaQuizReuseContext Empty { get; } = new((DateTimeOffset?)null);
    }

    private abstract class TriviaQuizReuseWorkflowTemplate<TResult>
    {
        public TResult Apply(TriviaQuiz quiz, TriviaQuizReuseContext context)
        {
            EnsureCurrentStateAllowsOperation(quiz.Status);
            EnsureUsageStateAllowsOperation(quiz.HasUsageHistory);
            return ApplyOperation(quiz, context);
        }

        protected virtual void EnsureCurrentStateAllowsOperation(TriviaQuizStatus status)
        {
        }

        protected virtual void EnsureUsageStateAllowsOperation(bool hasUsageHistory)
        {
        }

        protected abstract TResult ApplyOperation(TriviaQuiz quiz, TriviaQuizReuseContext context);
    }

    private sealed record ValidatedTriviaQuizAuthoring(string Title, string Description, IReadOnlyCollection<TriviaQuestion> Questions);
    private sealed record TriviaQuestionAuthoringDraft(
        int? QuestionId,
        string Prompt,
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

            var normalizedQuestions = NormalizeQuestions(questions);
            EnsureQuestionsSatisfyAuthoringRules(normalizedQuestions);

            return new ValidatedTriviaQuizAuthoring(
                title.Trim(),
                description.Trim(),
                normalizedQuestions);
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

        private static void EnsureQuestionsSatisfyAuthoringRules(IReadOnlyCollection<TriviaQuestion> questions)
        {
            foreach (var question in questions)
            {
                EnsureOptionCount(question.Options.Count);
                EnsureExactlyOneCorrectOption(question.Options);
            }
        }
    }

    private sealed class CreateTriviaQuizAuthoringTemplate : TriviaQuizAuthoringTemplate
    {
    }

    private sealed class UpdateTriviaQuizAuthoringTemplate : TriviaQuizAuthoringTemplate
    {
        protected override void EnsureEditable(TriviaQuizStatus status)
        {
            EnsureQuizEditable(status);
        }
    }

    private abstract class TriviaQuestionAuthoringTemplate
    {
        public TriviaQuestion Apply(TriviaQuiz quiz, TriviaQuestionAuthoringDraft draft)
        {
            EnsureEditable(quiz.Status);

            var targetQuestion = ResolveTargetQuestion(quiz, draft);
            var normalizedOptions = NormalizeOptions(draft.Options);

            EnsureOptionCount(normalizedOptions.Count);
            EnsureExactlyOneCorrectOption(normalizedOptions);
            EnsureDistinctOptionSequenceOrders(normalizedOptions);

            return BuildQuestion(targetQuestion, draft, normalizedOptions);
        }

        protected virtual void EnsureEditable(TriviaQuizStatus status)
        {
            EnsureQuizEditable(status);
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

        private static void EnsureDistinctOptionSequenceOrders(IEnumerable<TriviaOption> options)
        {
            var sequenceOrders = options.Select(option => option.SequenceOrder).ToArray();

            if (sequenceOrders.Length != sequenceOrders.Distinct().Count())
            {
                throw new TriviaOptionSequenceOrderMustBeUniqueException();
            }
        }
    }

    private abstract class TriviaQuestionRemovalTemplate
    {
        public TriviaQuestion Apply(TriviaQuiz quiz, int triviaQuestionId)
        {
            EnsureQuizEditable(quiz.Status);
            var question = FindQuestion(quiz, triviaQuestionId);
            RemoveQuestion(quiz, question);
            return question;
        }

        private static TriviaQuestion FindQuestion(TriviaQuiz quiz, int triviaQuestionId)
        {
            var question = quiz._questions.SingleOrDefault(q => q.Id == triviaQuestionId);

            if (question is null)
            {
                throw new TriviaQuestionNotFoundException(triviaQuestionId);
            }

            return question;
        }

        private static void RemoveQuestion(TriviaQuiz quiz, TriviaQuestion question)
        {
            quiz._questions.Remove(question);
        }

    }

    private sealed class RemoveTriviaQuestionAuthoringTemplate : TriviaQuestionRemovalTemplate
    {
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

    private sealed class DuplicateTriviaQuizWorkflowTemplate : TriviaQuizReuseWorkflowTemplate<TriviaQuiz>
    {
        protected override void EnsureCurrentStateAllowsOperation(TriviaQuizStatus status)
        {
            if (status == TriviaQuizStatus.Archived)
            {
                throw new TriviaQuizCannotBeArchivedInCurrentStateException(status);
            }
        }

        protected override TriviaQuiz ApplyOperation(TriviaQuiz quiz, TriviaQuizReuseContext context)
        {
            var duplicate = new TriviaQuiz(
                quiz.Title,
                quiz.Description,
                TriviaQuizStatus.Draft,
                publishedAt: null,
                quiz.Questions.Select(CloneQuestion).ToArray(),
                ResolveDuplicateLineageSourceId(quiz),
                hasUsageHistory: false);

            duplicate.AddDomainEvent(new TriviaQuizCreatedEvent(duplicate));
            quiz.AddDomainEvent(new TriviaQuizDuplicatedEvent(quiz, duplicate));

            return duplicate;
        }
    }

    private sealed class RetireTriviaQuizWorkflowTemplate : TriviaQuizReuseWorkflowTemplate<TriviaQuiz>
    {
        protected override void EnsureCurrentStateAllowsOperation(TriviaQuizStatus status)
        {
            if (status == TriviaQuizStatus.Archived)
            {
                throw new TriviaQuizCannotBeArchivedInCurrentStateException(status);
            }
        }

        protected override void EnsureUsageStateAllowsOperation(bool hasUsageHistory)
        {
            if (!hasUsageHistory)
            {
                throw new TriviaQuizCannotBeRetiredWithoutUsageHistoryException();
            }
        }

        protected override TriviaQuiz ApplyOperation(TriviaQuiz quiz, TriviaQuizReuseContext context)
        {
            ArchiveTemplate.Apply(quiz, context.ArchivedAt!.Value);
            return quiz;
        }
    }

    private sealed class DestructiveRemovalTriviaQuizWorkflowTemplate : TriviaQuizReuseWorkflowTemplate<TriviaQuiz>
    {
        protected override void EnsureUsageStateAllowsOperation(bool hasUsageHistory)
        {
            if (hasUsageHistory)
            {
                throw new TriviaQuizCannotBeDestructivelyRemovedAfterUsageException();
            }
        }

        protected override TriviaQuiz ApplyOperation(TriviaQuiz quiz, TriviaQuizReuseContext context)
        {
            return quiz;
        }
    }
}
