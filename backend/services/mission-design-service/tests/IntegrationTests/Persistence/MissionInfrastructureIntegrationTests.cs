using MediatR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Handlers;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;
using umbral_backend.Domain.Events;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

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
        triviaQuiz.CreatedBy.Should().BeNull();
        triviaQuiz.LastModifiedBy.Should().BeNull();
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
                    [
                        Domain.Entities.TriviaOption.Create("1810", 1, true),
                        Domain.Entities.TriviaOption.Create("1910", 2, false)
                    ]),
                Domain.Entities.TriviaQuestion.Create(
                    "Who signed the act?",
                    2,
                    [
                        Domain.Entities.TriviaOption.Create("Person A", 1, true),
                        Domain.Entities.TriviaOption.Create("Person B", 2, false)
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
        detail.Questions[1].Options.Select(option => option.OptionText).Should().Equal("Person A", "Person B");
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
