using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Infrastructure.Integrations.MissionDesign;
using umbral_backend.Infrastructure.IntegrationTests.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Integrations.MissionDesign;

public sealed class PublishedTriviaQuizSourceIntegrationTests
{
    [Fact]
    public async Task GetByIdAsync_WhenQuizExists_ReturnsMappedQuiz()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/trivias/{id:int}", (int id) =>
                Results.Ok(new
                {
                    Id = id,
                    Title = "Quiz Night",
                    Description = "Trivia detail",
                    Status = "Published",
                    IsSourceReady = true,
                    SourceTriviaQuizId = (int?)null,
                    HasUsageHistory = false,
                    IsDuplicate = false,
                    Questions = new[]
                    {
                        new
                        {
                            Id = 7,
                            Prompt = "Capital of France?",
                            SequenceOrder = 2,
                            IsActive = true,
                            Options = new[]
                            {
                                new { Id = 1, OptionText = "Paris", SequenceOrder = 2, IsCorrect = true },
                                new { Id = 2, OptionText = "Lyon", SequenceOrder = 1, IsCorrect = false }
                            },
                            ScoreValue = 50,
                            TimeLimitSeconds = 30,
                            Explanation = "Paris is the capital city."
                        }
                    }
                }));
        });

        var client = host.GetTestClient();
        var source = new PublishedTriviaQuizSource(client, TestCurrentUser.Default);

        var triviaQuiz = await source.GetByIdAsync(42, CancellationToken.None);

        triviaQuiz.Should().NotBeNull();
        triviaQuiz!.Id.Should().Be(42);
        triviaQuiz.Status.Should().Be("Published");
        triviaQuiz.Questions.Should().ContainSingle();
        triviaQuiz.Questions.Single().Options.Should().HaveCount(2);
        triviaQuiz.Questions.Single().Options.Should().ContainSingle(option => option.OptionText == "Paris" && option.IsCorrect);
    }

    [Fact]
    public async Task GetByIdAsync_WhenQuizIsNotPublished_StillReturnsQuizForApplicationGate()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/trivias/{id:int}", (int id) =>
                Results.Ok(new
                {
                    Id = id,
                    Title = "Draft Quiz",
                    Description = "Trivia detail",
                    Status = "Draft",
                    IsSourceReady = false,
                    SourceTriviaQuizId = (int?)null,
                    HasUsageHistory = false,
                    IsDuplicate = false,
                    Questions = new[]
                    {
                        new
                        {
                            Id = 8,
                            Prompt = "Pending question",
                            SequenceOrder = 1,
                            IsActive = true,
                            Options = new[]
                            {
                                new { Id = 3, OptionText = "A", SequenceOrder = 1, IsCorrect = true },
                                new { Id = 4, OptionText = "B", SequenceOrder = 2, IsCorrect = false }
                            },
                            ScoreValue = 10,
                            TimeLimitSeconds = 15,
                            Explanation = (string?)null
                        }
                    }
                }));
        });

        var source = new PublishedTriviaQuizSource(host.GetTestClient(), TestCurrentUser.Default);

        var triviaQuiz = await source.GetByIdAsync(21, CancellationToken.None);

        triviaQuiz.Should().NotBeNull();
        triviaQuiz!.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task GetByIdAsync_WhenQuizDoesNotExist_ReturnsNull()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/trivias/{id:int}", () => Results.StatusCode((int)HttpStatusCode.NotFound));
        });

        var source = new PublishedTriviaQuizSource(host.GetTestClient(), TestCurrentUser.Default);

        var triviaQuiz = await source.GetByIdAsync(404, CancellationToken.None);

        triviaQuiz.Should().BeNull();
    }

    private static async Task<IHost> BuildHostAsync(Action<WebApplication> configureRoutes)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });

        var app = builder.Build();
        configureRoutes(app);
        await app.StartAsync();
        return app;
    }
}
