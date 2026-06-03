using Microsoft.EntityFrameworkCore;
using MediatR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

[Collection("MissionDesignIntegrationTests")]
public sealed class MissionInfrastructureIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public MissionInfrastructureIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateMission_PersistsMissionAndPublishesCreatedEvent()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-01"));
        var handler = new CreateMissionCommandHandler(new MissionRepository(actContext));

        var result = await handler.Handle(
            new CreateMissionCommand(" Mission Beta ", " Persisted through postgres ", "Advanced", 60),
            CancellationToken.None);

        result.Status.Should().Be("Draft");

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is MissionCreatedEvent);

        await using var assertContext = BuildContext();
        var mission = await assertContext.Missions.SingleAsync(storedMission => storedMission.Id == result.Id);

        mission.Name.Should().Be("Mission Beta");
        mission.Description.Should().Be("Persisted through postgres");
        mission.Difficulty.Value.Should().Be("Advanced");
        mission.MaximumTime.Minutes.Should().Be(60);
        mission.IsActive.Should().BeTrue();
        mission.ArchivedAt.Should().BeNull();
        mission.ActivationState.ToString().Should().Be("Draft");
        mission.CreatedBy.Should().Be("admin-01");
        mission.LastModifiedBy.Should().Be("admin-01");
        mission.Created.Should().NotBe(default);
        mission.LastModified.Should().NotBe(default);
    }

    [Fact]
    public async Task UpdateMission_PersistsChangesAndPublishesUpdatedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new MissionRepository(setupContext);

        var mission = Domain.Entities.Mission.Create("Mission One", "Briefing", "Advanced", 45);
        await setupRepository.AddAsync(mission, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-02"));
        var handler = new UpdateMissionCommandHandler(new MissionRepository(actContext));

        var result = await handler.Handle(
            new UpdateMissionCommand(mission.Id, " Mission Two ", " Updated briefing ", "Beginner", 30),
            CancellationToken.None);

        result.Status.Should().Be("Draft");

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is MissionDetailsUpdatedEvent);

        await using var assertContext = BuildContext();
        var reloadedMission = await assertContext.Missions.SingleAsync(storedMission => storedMission.Id == mission.Id);

        reloadedMission.Name.Should().Be("Mission Two");
        reloadedMission.Description.Should().Be("Updated briefing");
        reloadedMission.Difficulty.Value.Should().Be("Beginner");
        reloadedMission.MaximumTime.Minutes.Should().Be(30);
        reloadedMission.IsActive.Should().BeTrue();
        reloadedMission.ArchivedAt.Should().BeNull();
        reloadedMission.ActivationState.ToString().Should().Be("Draft");
        reloadedMission.LastModifiedBy.Should().Be("admin-02");
    }

    [Fact]
    public async Task DeactivateMission_PersistsInactiveStateAndPublishesDeactivatedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new MissionRepository(setupContext);

        var mission = Domain.Entities.Mission.Create("Mission One", "Briefing", "Advanced", 45);
        await setupRepository.AddAsync(mission, CancellationToken.None);

        var archivedAt = new DateTimeOffset(2026, 5, 31, 18, 0, 0, TimeSpan.Zero);
        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-03"), new StubClock(archivedAt));
        var handler = new DeactivateMissionCommandHandler(new MissionRepository(actContext), new StubClock(archivedAt));

        await handler.Handle(new DeactivateMissionCommand(mission.Id), CancellationToken.None);

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is MissionDeactivatedEvent);

        await using var assertContext = BuildContext();
        var reloadedMission = await assertContext.Missions.SingleAsync(storedMission => storedMission.Id == mission.Id);

        reloadedMission.IsActive.Should().BeFalse();
        reloadedMission.ArchivedAt.Should().Be(archivedAt);
        reloadedMission.ActivationState.ToString().Should().Be("Inactive");
        reloadedMission.LastModifiedBy.Should().Be("admin-03");
    }

    [Fact]
    public async Task GetMissionCatalogAndDetail_ReflectCurrentActivationState()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var repository = new MissionRepository(setupContext);

        var activeMission = Domain.Entities.Mission.Create("Bravo Mission", "Second mission", "Advanced", 60);
        var inactiveMission = Domain.Entities.Mission.Create("Alpha Mission", "First mission", "Intermediate", 30);
        await repository.AddAsync(activeMission, CancellationToken.None);
        await repository.AddAsync(inactiveMission, CancellationToken.None);

        inactiveMission.Deactivate(new DateTimeOffset(2026, 5, 31, 10, 0, 0, TimeSpan.Zero));
        await repository.UpdateAsync(inactiveMission, CancellationToken.None);

        await using var queryContext = BuildContext();
        var readRepository = new MissionReadModelRepository(queryContext);
        var catalogHandler = new GetMissionCatalogQueryHandler(readRepository);
        var detailHandler = new GetMissionDetailQueryHandler(readRepository);

        var catalog = await catalogHandler.Handle(new GetMissionCatalogQuery(), CancellationToken.None);
        var detail = await detailHandler.Handle(new GetMissionDetailQuery(inactiveMission.Id), CancellationToken.None);

        catalog.Select(item => (item.Name, item.Status))
            .Should().Equal(
                ("Alpha Mission", "Inactive"),
                ("Bravo Mission", "Draft"));

        detail.Id.Should().Be(inactiveMission.Id);
        detail.Name.Should().Be("Alpha Mission");
        detail.Description.Should().Be("First mission");
        detail.Difficulty.Should().Be("Intermediate");
        detail.MaximumTimeMinutes.Should().Be(30);
        detail.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task CreateTriviaQuiz_PersistsTriviaAggregateAndPublishesCreatedEvent()
    {
        await using var resetContext = BuildContext();
        await ResetDatabaseAsync(resetContext);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-11"));
        var handler = new CreateTriviaQuizCommandHandler(new TriviaQuizRepository(actContext));

        var result = await handler.Handle(
            new CreateTriviaQuizCommand(
                " General Knowledge ",
                " Initial trivia authoring ",
                [
                    new TriviaQuestionInput(
                        " Capital of France? ",
                        2,
                        true,
                        [
                            new TriviaOptionInput(" Berlin ", 2, false),
                            new TriviaOptionInput(" Paris ", 1, true)
                        ]),
                    new TriviaQuestionInput(
                        " 2 + 2 = ? ",
                        1,
                        true,
                        [
                            new TriviaOptionInput(" 4 ", 1, true),
                            new TriviaOptionInput(" 5 ", 2, false)
                        ])
                ]),
            CancellationToken.None);

        result.Status.Should().Be("Draft");
        result.Questions.Select(question => question.SequenceOrder).Should().Equal(1, 2);
        result.Questions[0].Options.Select(option => option.SequenceOrder).Should().Equal(1, 2);

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TriviaQuizCreatedEvent);

        await using var assertContext = BuildContext();
        var triviaQuiz = await assertContext.TriviaQuizzes
            .AsNoTracking()
            .Include(storedQuiz => storedQuiz.Questions)
            .ThenInclude(storedQuestion => storedQuestion.Options)
            .SingleAsync(storedQuiz => storedQuiz.Id == result.Id);

        triviaQuiz.Title.Should().Be("General Knowledge");
        triviaQuiz.Description.Should().Be("Initial trivia authoring");
        triviaQuiz.Status.Should().Be(Domain.Enums.TriviaQuizStatus.Draft);
        triviaQuiz.CreatedBy.Should().Be("admin-11");
        triviaQuiz.LastModifiedBy.Should().Be("admin-11");
        triviaQuiz.Created.Should().NotBe(default);
        triviaQuiz.LastModified.Should().NotBe(default);
        triviaQuiz.Questions.Select(question => question.Prompt)
            .Should().Equal("2 + 2 = ?", "Capital of France?");
        triviaQuiz.Questions.First().Options.Select(option => option.OptionText)
            .Should().Equal("4", "5");
    }

    [Fact]
    public async Task UpdateTriviaQuiz_PersistsQuestionsAndPublishesUpdatedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new TriviaQuizRepository(setupContext);

        var triviaQuiz = Domain.Entities.TriviaQuiz.Create(
            "Science",
            "Original quiz",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Original question",
                    1,
                    [
                        Domain.Entities.TriviaOption.Create("Option A", 1, true),
                        Domain.Entities.TriviaOption.Create("Option B", 2, false)
                    ])
            ]);

        await setupRepository.AddAsync(triviaQuiz, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-12"));
        var handler = new UpdateTriviaQuizCommandHandler(new TriviaQuizRepository(actContext));

        var result = await handler.Handle(
            new UpdateTriviaQuizCommand(
                triviaQuiz.Id,
                " Science Reloaded ",
                " Updated quiz description ",
                [
                    new TriviaQuestionInput(
                        "Updated first question",
                        1,
                        true,
                        [
                            new TriviaOptionInput("Correct", 1, true),
                            new TriviaOptionInput("Incorrect", 2, false)
                        ]),
                    new TriviaQuestionInput(
                        "Updated second question",
                        2,
                        false,
                        [
                            new TriviaOptionInput("Yes", 1, true),
                            new TriviaOptionInput("No", 2, false)
                        ])
                ]),
            CancellationToken.None);

        result.Title.Should().Be("Science Reloaded");
        result.Questions.Should().HaveCount(2);

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TriviaQuizDetailsUpdatedEvent);

        await using var assertContext = BuildContext();
        var reloadedQuiz = await assertContext.TriviaQuizzes
            .AsNoTracking()
            .Include(storedQuiz => storedQuiz.Questions)
            .ThenInclude(storedQuestion => storedQuestion.Options)
            .SingleAsync(storedQuiz => storedQuiz.Id == triviaQuiz.Id);

        reloadedQuiz.Title.Should().Be("Science Reloaded");
        reloadedQuiz.Description.Should().Be("Updated quiz description");
        reloadedQuiz.Status.Should().Be(Domain.Enums.TriviaQuizStatus.Draft);
        reloadedQuiz.Questions.Should().HaveCount(2);
        reloadedQuiz.Questions.Select(question => question.SequenceOrder).Should().Equal(1, 2);
        reloadedQuiz.Questions.Last().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AddTriviaQuestion_PersistsQuestionMetadataAndPublishesAddedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new TriviaQuizRepository(setupContext);

        var triviaQuiz = Domain.Entities.TriviaQuiz.Create("Geography", "Quiz ready for authoring");
        await setupRepository.AddAsync(triviaQuiz, CancellationToken.None);

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-13"));
        var handler = new AddTriviaQuestionCommandHandler(new TriviaQuizRepository(actContext));

        var result = await handler.Handle(
            new AddTriviaQuestionCommand(
                triviaQuiz.Id,
                " Highest mountain? ",
                1,
                100,
                45,
                " Because Everest is the tallest above sea level. ",
                true,
                [
                    new TriviaOptionInput(" Everest ", 1, true),
                    new TriviaOptionInput(" K2 ", 2, false),
                    new TriviaOptionInput(" Kilimanjaro ", 3, false),
                    new TriviaOptionInput(" Aconcagua ", 4, false)
                ]),
            CancellationToken.None);

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TriviaQuestionAddedEvent);

        result.Questions.Should().ContainSingle();
        result.Questions[0].ScoreValue.Should().Be(100);
        result.Questions[0].TimeLimitSeconds.Should().Be(45);
        result.Questions[0].Explanation.Should().Be("Because Everest is the tallest above sea level.");

        await using var assertContext = BuildContext();
        var reloadedQuiz = await assertContext.TriviaQuizzes
            .AsNoTracking()
            .Include(storedQuiz => storedQuiz.Questions)
            .ThenInclude(storedQuestion => storedQuestion.Options)
            .SingleAsync(storedQuiz => storedQuiz.Id == triviaQuiz.Id);

        reloadedQuiz.Questions.Should().ContainSingle();
        var question = reloadedQuiz.Questions.Single();
        question.Prompt.Should().Be("Highest mountain?");
        question.ScoreValue.Should().Be(100);
        question.TimeLimit!.Seconds.Should().Be(45);
        question.Explanation.Should().Be("Because Everest is the tallest above sea level.");
        question.Options.Should().HaveCount(4);
        question.Options.Count(option => option.IsCorrect).Should().Be(1);
        question.Options.Single(option => option.IsCorrect).OptionText.Should().Be("Everest");
    }

    [Fact]
    public async Task UpdateTriviaQuestion_PersistsQuestionMetadataAndPublishesUpdatedEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new TriviaQuizRepository(setupContext);

        var triviaQuiz = Domain.Entities.TriviaQuiz.Create(
            "Science",
            "Question updates",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Original question",
                    1,
                    100,
                    30,
                    "Original explanation",
                    [
                        Domain.Entities.TriviaOption.Create("Option A", 1, true),
                        Domain.Entities.TriviaOption.Create("Option B", 2, false)
                    ])
            ]);

        await setupRepository.AddAsync(triviaQuiz, CancellationToken.None);
        var existingQuestionId = triviaQuiz.Questions.Single().Id;

        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-14"));
        var handler = new UpdateTriviaQuestionCommandHandler(new TriviaQuizRepository(actContext));

        var result = await handler.Handle(
            new UpdateTriviaQuestionCommand(
                triviaQuiz.Id,
                existingQuestionId,
                " Updated question ",
                2,
                100,
                60,
                " Updated explanation ",
                false,
                [
                    new TriviaOptionInput("Wrong", 1, false),
                    new TriviaOptionInput("Right", 2, true),
                    new TriviaOptionInput("Almost", 3, false)
                ]),
            CancellationToken.None);

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TriviaQuestionUpdatedEvent);

        result.Questions.Should().ContainSingle();
        result.Questions[0].SequenceOrder.Should().Be(2);
        result.Questions[0].ScoreValue.Should().Be(100);
        result.Questions[0].TimeLimitSeconds.Should().Be(60);
        result.Questions[0].Explanation.Should().Be("Updated explanation");

        await using var assertContext = BuildContext();
        var reloadedQuiz = await assertContext.TriviaQuizzes
            .AsNoTracking()
            .Include(storedQuiz => storedQuiz.Questions)
            .ThenInclude(storedQuestion => storedQuestion.Options)
            .SingleAsync(storedQuiz => storedQuiz.Id == triviaQuiz.Id);

        var question = reloadedQuiz.Questions.Single();
        question.Prompt.Should().Be("Updated question");
        question.SequenceOrder.Should().Be(2);
        question.ScoreValue.Should().Be(100);
        question.TimeLimit!.Seconds.Should().Be(60);
        question.Explanation.Should().Be("Updated explanation");
        question.IsActive.Should().BeFalse();
        question.Options.Should().HaveCount(3);
        question.Options.Count(option => option.IsCorrect).Should().Be(1);
        question.Options.Single(option => option.IsCorrect).OptionText.Should().Be("Right");
    }

    [Fact]
    public async Task GetTriviaCatalogAndDetail_ReflectPersistedQuestionShape()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var repository = new TriviaQuizRepository(setupContext);

        var firstQuiz = Domain.Entities.TriviaQuiz.Create(
            "History",
            "Historic facts",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "When was the city founded?",
                    1,
                    100,
                    20,
                    "The first charter date is the accepted answer.",
                    [
                        Domain.Entities.TriviaOption.Create("1810", 1, true),
                        Domain.Entities.TriviaOption.Create("1910", 2, false)
                    ])
            ]);

        var secondQuiz = Domain.Entities.TriviaQuiz.Create(
            "Sports",
            "Sports trivia",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "How many players start?",
                    1,
                    100,
                    15,
                    null,
                    [
                        Domain.Entities.TriviaOption.Create("11", 1, true),
                        Domain.Entities.TriviaOption.Create("10", 2, false)
                    ])
            ]);

        await repository.AddAsync(firstQuiz, CancellationToken.None);
        await repository.AddAsync(secondQuiz, CancellationToken.None);

        firstQuiz.UpdateDetails(
            "History",
            "Historic facts updated",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "When was the city founded?",
                    1,
                    100,
                    20,
                    "The first charter date is the accepted answer.",
                    [
                        Domain.Entities.TriviaOption.Create("1810", 1, true),
                        Domain.Entities.TriviaOption.Create("1910", 2, false)
                    ]),
                Domain.Entities.TriviaQuestion.Create(
                    "Who signed the act?",
                    2,
                    100,
                    40,
                    "The signer appears in the independence record.",
                    [
                        Domain.Entities.TriviaOption.Create("Person A", 1, true),
                        Domain.Entities.TriviaOption.Create("Person B", 2, false),
                        Domain.Entities.TriviaOption.Create("Person C", 3, false),
                        Domain.Entities.TriviaOption.Create("Person D", 4, false)
                    ])
            ]);

        await repository.UpdateAsync(firstQuiz, CancellationToken.None);

        await using var queryContext = BuildContext();
        var readRepository = new TriviaQuizReadModelRepository(queryContext);
        var catalogHandler = new GetTriviaCatalogQueryHandler(readRepository);
        var detailHandler = new GetTriviaDetailQueryHandler(readRepository);

        var catalog = await catalogHandler.Handle(new GetTriviaCatalogQuery(), CancellationToken.None);
        var detail = await detailHandler.Handle(new GetTriviaDetailQuery(firstQuiz.Id), CancellationToken.None);

        catalog.Select(item => item.Title).Should().Equal("History", "Sports");
        detail.Title.Should().Be("History");
        detail.Description.Should().Be("Historic facts updated");
        detail.Status.Should().Be("Draft");
        detail.Questions.Select(question => question.SequenceOrder).Should().Equal(1, 2);
        detail.Questions[0].ScoreValue.Should().Be(100);
        detail.Questions[0].TimeLimitSeconds.Should().Be(20);
        detail.Questions[0].Explanation.Should().Be("The first charter date is the accepted answer.");
        detail.Questions[1].ScoreValue.Should().Be(100);
        detail.Questions[1].TimeLimitSeconds.Should().Be(40);
        detail.Questions[1].Explanation.Should().Be("The signer appears in the independence record.");
        detail.Questions[1].Options.Select(option => option.OptionText).Should().Equal("Person A", "Person B", "Person C", "Person D");
        detail.Questions[1].Options.Should().HaveCount(4);
        detail.Questions[1].Options.Count(option => option.IsCorrect).Should().Be(1);
    }

    [Fact]
    public async Task PublishTriviaQuiz_PersistsPublishedStateAndPublishesLifecycleEvent()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new TriviaQuizRepository(setupContext);

        var triviaQuiz = Domain.Entities.TriviaQuiz.Create(
            "Science Finals",
            "Ready to publish",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "What is H2O?",
                    1,
                    100,
                    30,
                    "Water.",
                    [
                        Domain.Entities.TriviaOption.Create("Water", 1, true),
                        Domain.Entities.TriviaOption.Create("Oxygen", 2, false)
                    ])
            ]);

        await setupRepository.AddAsync(triviaQuiz, CancellationToken.None);

        var publishedAt = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-15"), new StubClock(publishedAt));
        var handler = new PublishTriviaQuizCommandHandler(new TriviaQuizRepository(actContext), new StubClock(publishedAt));

        var result = await handler.Handle(new PublishTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        result.Status.Should().Be("Published");

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TriviaQuizPublishedEvent);

        await using var assertContext = BuildContext();
        var reloadedQuiz = await assertContext.TriviaQuizzes.SingleAsync(storedQuiz => storedQuiz.Id == triviaQuiz.Id);

        reloadedQuiz.Status.Should().Be(TriviaQuizStatus.Published);
        reloadedQuiz.PublishedAt.Should().Be(publishedAt);
        reloadedQuiz.LastModifiedBy.Should().Be("admin-15");
    }

    [Fact]
    public async Task PublishTriviaQuiz_WhenQuizIsNotReady_DoesNotPersistStateChange()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new TriviaQuizRepository(setupContext);

        var triviaQuiz = Domain.Entities.TriviaQuiz.Create(
            "Incomplete Quiz",
            "Missing publish metadata",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Incomplete question",
                    1,
                    [
                        Domain.Entities.TriviaOption.Create("Correct", 1, true),
                        Domain.Entities.TriviaOption.Create("Incorrect", 2, false)
                    ])
            ]);

        await setupRepository.AddAsync(triviaQuiz, CancellationToken.None);

        var publishedAt = new DateTimeOffset(2026, 6, 2, 12, 30, 0, TimeSpan.Zero);
        await using var actContext = BuildContext(new CapturingMediator(), new StubCurrentUser("admin-16"), new StubClock(publishedAt));
        var handler = new PublishTriviaQuizCommandHandler(new TriviaQuizRepository(actContext), new StubClock(publishedAt));

        var act = () => handler.Handle(new PublishTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuestionScoreValueRequiredToPublishException>();

        await using var assertContext = BuildContext();
        var reloadedQuiz = await assertContext.TriviaQuizzes.SingleAsync(storedQuiz => storedQuiz.Id == triviaQuiz.Id);

        reloadedQuiz.Status.Should().Be(TriviaQuizStatus.Draft);
        reloadedQuiz.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveTriviaQuiz_PersistsArchivedStateAndRetainsPublicationHistory()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var setupRepository = new TriviaQuizRepository(setupContext);

        var publishedAt = new DateTimeOffset(2026, 6, 2, 13, 0, 0, TimeSpan.Zero);
        var triviaQuiz = Domain.Entities.TriviaQuiz.Create(
            "History Finals",
            "Previously published",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Who discovered America?",
                    1,
                    100,
                    30,
                    "Expected baseline answer.",
                    [
                        Domain.Entities.TriviaOption.Create("Christopher Columbus", 1, true),
                        Domain.Entities.TriviaOption.Create("Simón Bolívar", 2, false)
                    ])
            ]);

        triviaQuiz.Publish(publishedAt);
        await setupRepository.AddAsync(triviaQuiz, CancellationToken.None);

        var archivedAt = new DateTimeOffset(2026, 6, 2, 14, 0, 0, TimeSpan.Zero);
        var mediator = new CapturingMediator();
        await using var actContext = BuildContext(mediator, new StubCurrentUser("admin-17"), new StubClock(archivedAt));
        var handler = new ArchiveTriviaQuizCommandHandler(new TriviaQuizRepository(actContext), new StubClock(archivedAt));

        var result = await handler.Handle(new ArchiveTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        result.Status.Should().Be("Archived");

        mediator.PublishedNotifications
            .Should().ContainSingle(notification => notification is TriviaQuizArchivedEvent);

        await using var assertContext = BuildContext();
        var reloadedQuiz = await assertContext.TriviaQuizzes.SingleAsync(storedQuiz => storedQuiz.Id == triviaQuiz.Id);

        reloadedQuiz.Status.Should().Be(TriviaQuizStatus.Archived);
        reloadedQuiz.PublishedAt.Should().Be(publishedAt);
        reloadedQuiz.LastModifiedBy.Should().Be("admin-17");
    }

    [Fact]
    public async Task GetTriviaCatalogAndDetail_ReflectLifecycleStateAndPublishedOnlySourceReadiness()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var repository = new TriviaQuizRepository(setupContext);

        var draftQuiz = Domain.Entities.TriviaQuiz.Create(
            "Draft Trivia",
            "Still in draft",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Draft question",
                    1,
                    100,
                    20,
                    "Draft explanation",
                    [
                        Domain.Entities.TriviaOption.Create("Draft correct", 1, true),
                        Domain.Entities.TriviaOption.Create("Draft incorrect", 2, false)
                    ])
            ]);

        var publishedQuiz = Domain.Entities.TriviaQuiz.Create(
            "Published Trivia",
            "Available for sessions",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Published question",
                    1,
                    100,
                    20,
                    "Published explanation",
                    [
                        Domain.Entities.TriviaOption.Create("Published correct", 1, true),
                        Domain.Entities.TriviaOption.Create("Published incorrect", 2, false)
                    ])
            ]);
        publishedQuiz.Publish(new DateTimeOffset(2026, 6, 2, 15, 0, 0, TimeSpan.Zero));

        var archivedQuiz = Domain.Entities.TriviaQuiz.Create(
            "Archived Trivia",
            "Withdrawn from future sessions",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Archived question",
                    1,
                    100,
                    20,
                    "Archived explanation",
                    [
                        Domain.Entities.TriviaOption.Create("Archived correct", 1, true),
                        Domain.Entities.TriviaOption.Create("Archived incorrect", 2, false)
                    ])
            ]);
        archivedQuiz.Publish(new DateTimeOffset(2026, 6, 2, 16, 0, 0, TimeSpan.Zero));
        archivedQuiz.Archive(new DateTimeOffset(2026, 6, 2, 17, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(draftQuiz, CancellationToken.None);
        await repository.AddAsync(publishedQuiz, CancellationToken.None);
        await repository.AddAsync(archivedQuiz, CancellationToken.None);

        await using var queryContext = BuildContext();
        var readRepository = new TriviaQuizReadModelRepository(queryContext);
        var catalogHandler = new GetTriviaCatalogQueryHandler(readRepository);
        var detailHandler = new GetTriviaDetailQueryHandler(readRepository);

        var catalog = await catalogHandler.Handle(new GetTriviaCatalogQuery(), CancellationToken.None);
        var publishedDetail = await detailHandler.Handle(new GetTriviaDetailQuery(publishedQuiz.Id), CancellationToken.None);
        var archivedDetail = await detailHandler.Handle(new GetTriviaDetailQuery(archivedQuiz.Id), CancellationToken.None);

        catalog.Should().Contain(item => item.Id == draftQuiz.Id && item.Status == "Draft");
        catalog.Should().Contain(item => item.Id == publishedQuiz.Id && item.Status == "Published");
        catalog.Should().Contain(item => item.Id == archivedQuiz.Id && item.Status == "Archived");

        publishedDetail.Status.Should().Be("Published");
        archivedDetail.Status.Should().Be("Archived");

        var sourceReadyQuizIds = await queryContext.TriviaQuizzes
            .AsNoTracking()
            .Where(triviaQuiz => triviaQuiz.Status == TriviaQuizStatus.Published)
            .Select(triviaQuiz => triviaQuiz.Id)
            .ToListAsync();

        sourceReadyQuizIds.Should().ContainSingle().Which.Should().Be(publishedQuiz.Id);
    }

    [Fact]
    public async Task DuplicateTriviaQuiz_PersistsSeparateAuthoringCopyWithLineageAndReusableStructure()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var repository = new TriviaQuizRepository(setupContext);

        var sourceQuiz = Domain.Entities.TriviaQuiz.Create(
            "Campus History",
            "Original source quiz",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "When was the campus founded?",
                    1,
                    100,
                    30,
                    "Use the first official founding record.",
                    [
                        Domain.Entities.TriviaOption.Create("1810", 1, true),
                        Domain.Entities.TriviaOption.Create("1910", 2, false)
                    ]),
                Domain.Entities.TriviaQuestion.Create(
                    "Which building came first?",
                    2,
                    100,
                    45,
                    "The main hall predates the library.",
                    [
                        Domain.Entities.TriviaOption.Create("Main Hall", 1, true),
                        Domain.Entities.TriviaOption.Create("Library", 2, false)
                    ])
            ]);
        sourceQuiz.Publish(new DateTimeOffset(2026, 6, 3, 9, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(sourceQuiz, CancellationToken.None);

        await using var actContext = BuildContext(new CapturingMediator(), new StubCurrentUser("admin-18"));
        var handler = new DuplicateTriviaQuizCommandHandler(
            new TriviaQuizRepository(actContext),
            new StubClock(new DateTimeOffset(2026, 6, 3, 9, 30, 0, TimeSpan.Zero)));

        var result = await handler.Handle(new DuplicateTriviaQuizCommand(sourceQuiz.Id), CancellationToken.None);

        result.Id.Should().NotBe(sourceQuiz.Id);
        result.Status.Should().Be("Draft");
        result.SourceTriviaQuizId.Should().Be(sourceQuiz.Id);
        result.IsDuplicate.Should().BeTrue();
        result.HasUsageHistory.Should().BeFalse();
        result.Questions.Should().HaveCount(2);

        await using var assertContext = BuildContext();
        var storedQuizzes = await assertContext.TriviaQuizzes
            .AsNoTracking()
            .Include(triviaQuiz => triviaQuiz.Questions)
            .ThenInclude(question => question.Options)
            .OrderBy(triviaQuiz => triviaQuiz.Id)
            .ToListAsync();

        storedQuizzes.Should().HaveCount(2);

        var persistedSource = storedQuizzes.Single(triviaQuiz => triviaQuiz.Id == sourceQuiz.Id);
        var persistedDuplicate = storedQuizzes.Single(triviaQuiz => triviaQuiz.Id == result.Id);

        persistedSource.SourceTriviaQuizId.Should().BeNull();
        persistedSource.Status.Should().Be(TriviaQuizStatus.Published);
        persistedSource.Questions.Select(question => question.Prompt)
            .Should().Equal("When was the campus founded?", "Which building came first?");

        persistedDuplicate.SourceTriviaQuizId.Should().Be(sourceQuiz.Id);
        persistedDuplicate.HasUsageHistory.Should().BeFalse();
        persistedDuplicate.Status.Should().Be(TriviaQuizStatus.Draft);
        persistedDuplicate.Questions.Select(question => question.Prompt)
            .Should().Equal("When was the campus founded?", "Which building came first?");
        persistedDuplicate.Questions.Select(question => question.Id)
            .Should().OnlyHaveUniqueItems();
        persistedDuplicate.Questions.SelectMany(question => question.Options).Select(option => option.Id)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task DeleteTriviaQuiz_WhenQuizHasUsageHistory_RejectsDestructiveRemovalAndKeepsRecord()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var repository = new TriviaQuizRepository(setupContext);

        var triviaQuiz = Domain.Entities.TriviaQuiz.Create("Used Quiz", "Must stay for history");
        triviaQuiz.MarkAsUsedInSession();
        await repository.AddAsync(triviaQuiz, CancellationToken.None);

        await using var actContext = BuildContext();
        var handler = new DeleteTriviaQuizCommandHandler(new TriviaQuizRepository(actContext));

        var act = () => handler.Handle(new DeleteTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TriviaQuizCannotBeDestructivelyRemovedAfterUsageException>();

        await using var assertContext = BuildContext();
        var storedQuiz = await assertContext.TriviaQuizzes
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == triviaQuiz.Id);

        storedQuiz.Should().NotBeNull();
        storedQuiz!.HasUsageHistory.Should().BeTrue();
    }

    [Fact]
    public async Task RetireTriviaQuiz_WhenUsed_PersistsArchivedStateWithoutBreakingHistoricalIdentity()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var repository = new TriviaQuizRepository(setupContext);

        var triviaQuiz = Domain.Entities.TriviaQuiz.Create(
            "Used Published Quiz",
            "Can no longer be used for new sessions",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Who keeps the record?",
                    1,
                    100,
                    30,
                    "The archive keeps the original identity.",
                    [
                        Domain.Entities.TriviaOption.Create("The original quiz", 1, true),
                        Domain.Entities.TriviaOption.Create("The duplicate only", 2, false)
                    ])
            ]);
        triviaQuiz.Publish(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
        triviaQuiz.MarkAsUsedInSession();
        await repository.AddAsync(triviaQuiz, CancellationToken.None);

        var archivedAt = new DateTimeOffset(2026, 6, 3, 11, 0, 0, TimeSpan.Zero);
        await using var actContext = BuildContext(new CapturingMediator(), new StubCurrentUser("admin-19"), new StubClock(archivedAt));
        var handler = new RetireTriviaQuizCommandHandler(new TriviaQuizRepository(actContext), new StubClock(archivedAt));

        var result = await handler.Handle(new RetireTriviaQuizCommand(triviaQuiz.Id), CancellationToken.None);

        result.Id.Should().Be(triviaQuiz.Id);
        result.Status.Should().Be("Archived");
        result.HasUsageHistory.Should().BeTrue();
        result.SourceTriviaQuizId.Should().BeNull();

        await using var assertContext = BuildContext();
        var storedQuiz = await assertContext.TriviaQuizzes
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == triviaQuiz.Id);

        storedQuiz.Id.Should().Be(triviaQuiz.Id);
        storedQuiz.Status.Should().Be(TriviaQuizStatus.Archived);
        storedQuiz.HasUsageHistory.Should().BeTrue();
        storedQuiz.PublishedAt.Should().Be(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
        storedQuiz.SourceTriviaQuizId.Should().BeNull();
    }

    [Fact]
    public async Task GetTriviaCatalogAndDetail_ExposeLineageAndUsageStateAfterDuplicateAndRetireFlows()
    {
        await using var setupContext = BuildContext();
        await ResetDatabaseAsync(setupContext);
        var repository = new TriviaQuizRepository(setupContext);

        var sourceQuiz = Domain.Entities.TriviaQuiz.Create(
            "Source Quiz",
            "Original lineage root",
            [
                Domain.Entities.TriviaQuestion.Create(
                    "Question one",
                    1,
                    100,
                    20,
                    "First explanation",
                    [
                        Domain.Entities.TriviaOption.Create("Correct", 1, true),
                        Domain.Entities.TriviaOption.Create("Incorrect", 2, false)
                    ])
            ]);
        sourceQuiz.Publish(new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero));
        sourceQuiz.MarkAsUsedInSession();
        await repository.AddAsync(sourceQuiz, CancellationToken.None);

        var duplicateQuiz = sourceQuiz.Duplicate();
        await repository.AddAsync(duplicateQuiz, CancellationToken.None);

        sourceQuiz.RetireFromFutureUse(new DateTimeOffset(2026, 6, 3, 13, 0, 0, TimeSpan.Zero));
        await repository.UpdateAsync(sourceQuiz, CancellationToken.None);

        await using var queryContext = BuildContext();
        var readRepository = new TriviaQuizReadModelRepository(queryContext);
        var catalog = await readRepository.GetTriviaCatalogAsync(CancellationToken.None);
        var sourceDetail = await readRepository.GetTriviaDetailAsync(sourceQuiz.Id, CancellationToken.None);
        var duplicateDetail = await readRepository.GetTriviaDetailAsync(duplicateQuiz.Id, CancellationToken.None);

        catalog.Should().Contain(item =>
            item.Id == sourceQuiz.Id &&
            item.Status == "Archived" &&
            item.HasUsageHistory &&
            item.SourceTriviaQuizId == null &&
            !item.IsDuplicate);

        catalog.Should().Contain(item =>
            item.Id == duplicateQuiz.Id &&
            item.Status == "Draft" &&
            !item.HasUsageHistory &&
            item.SourceTriviaQuizId == sourceQuiz.Id &&
            item.IsDuplicate);

        sourceDetail.Should().NotBeNull();
        sourceDetail!.Status.Should().Be("Archived");
        sourceDetail.HasUsageHistory.Should().BeTrue();
        sourceDetail.SourceTriviaQuizId.Should().BeNull();
        sourceDetail.IsDuplicate.Should().BeFalse();

        duplicateDetail.Should().NotBeNull();
        duplicateDetail!.Status.Should().Be("Draft");
        duplicateDetail.HasUsageHistory.Should().BeFalse();
        duplicateDetail.SourceTriviaQuizId.Should().Be(sourceQuiz.Id);
        duplicateDetail.IsDuplicate.Should().BeTrue();
        duplicateDetail.Questions.Should().ContainSingle();
    }

    private static async Task ResetDatabaseAsync(ApplicationDbContext context)
    {
        await context.TriviaQuizzes.ExecuteDeleteAsync();
        await context.Missions.ExecuteDeleteAsync();
    }

    private ApplicationDbContext BuildContext(
        IMediator? mediator = null,
        ICurrentUser? currentUser = null,
        IClock? clock = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.ConnectionString);

        optionsBuilder.AddInterceptors(
            new AuditableEntityInterceptor(
                currentUser ?? new StubCurrentUser(null),
                clock is null ? TimeProvider.System : new StubTimeProvider(clock.UtcNow)),
            new DispatchDomainEventsInterceptor(mediator ?? new NoOpMediator()));

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private sealed record StubCurrentUser(string? Id) : ICurrentUser
    {
        public List<string>? Roles => ["Administrator"];
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public StubTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class CapturingMediator : IMediator
    {
        public List<object> PublishedNotifications { get; } = new();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            PublishedNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (notification is not null)
            {
                PublishedNotifications.Add(notification);
            }

            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NoOpMediator : IMediator
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
