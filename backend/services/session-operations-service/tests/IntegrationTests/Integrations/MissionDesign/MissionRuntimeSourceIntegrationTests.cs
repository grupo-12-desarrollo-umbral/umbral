using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Infrastructure.IntegrationTests.Persistence;
using umbral_backend.Infrastructure.Integrations.MissionDesign;

namespace umbral_backend.Infrastructure.IntegrationTests.Integrations.MissionDesign;

public sealed class MissionRuntimeSourceIntegrationTests
{
    [Fact]
    public async Task GetByIdAsync_WhenMissionRuntimeExists_ReturnsResolvedRuntimePlan()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/missions/{id:int}/runtime-plan", (int id) =>
                Results.Ok(new
                {
                    Title = $"Mission {id}",
                    MaximumTime = 35,
                    Stages = new[]
                    {
                        new
                        {
                            Title = "Stage One",
                            SequenceOrder = 1,
                            Substages = new object[]
                            {
                                new
                                {
                                    Title = "Treasure Hunt",
                                    SequenceOrder = 1,
                                    PlayMode = "TreasureHunt",
                                    WinnerScore = 100,
                                    Targets = new[]
                                    {
                                        new
                                        {
                                            Name = "Target Alpha",
                                            QrCode = "QR-ALPHA",
                                            SequenceOrder = 1,
                                            IsActive = true,
                                            Clue = new
                                            {
                                                Text = "Look under the stairs",
                                                VisibilityPolicy = "AfterPreviousTarget"
                                            }
                                        }
                                    },
                                    TriviaQuestions = Array.Empty<object>()
                                },
                                new
                                {
                                    Title = "Trivia Round",
                                    SequenceOrder = 2,
                                    PlayMode = "Trivia",
                                    WinnerScore = (int?)null,
                                    Targets = Array.Empty<object>(),
                                    TriviaQuestions = new[]
                                    {
                                        new
                                        {
                                            Prompt = "Capital of France?",
                                            SequenceOrder = 1,
                                            ScoreValue = 50,
                                            TimeLimitSeconds = 30,
                                            Explanation = "Paris is the capital city.",
                                            Options = new[]
                                            {
                                                new { OptionText = "Paris", SequenceOrder = 1, IsCorrect = true },
                                                new { OptionText = "Lyon", SequenceOrder = 2, IsCorrect = false }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }));
        });

        var source = new MissionRuntimeSource(host.GetTestClient(), TestCurrentUser.Default);

        var runtime = await source.GetByIdAsync(42, CancellationToken.None);

        runtime.Should().NotBeNull();
        runtime!.Title.Should().Be("Mission 42");
        runtime.MaximumTime.Should().Be(35);
        runtime.Stages.Should().ContainSingle();

        var treasureHuntSubstage = runtime.Stages.Single().Substages.Single(substage => substage.PlayMode == "TreasureHunt");
        treasureHuntSubstage.WinnerScore.Should().Be(100);
        treasureHuntSubstage.Targets.Should().ContainSingle();
        treasureHuntSubstage.Targets.Single().Clue!.Text.Should().Be("Look under the stairs");

        var triviaSubstage = runtime.Stages.Single().Substages.Single(substage => substage.PlayMode == "Trivia");
        triviaSubstage.TriviaQuestions.Should().ContainSingle();
        triviaSubstage.TriviaQuestions.Single().Options.Should().ContainSingle(option => option.OptionText == "Paris" && option.IsCorrect);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissionDoesNotExist_ReturnsNull()
    {
        using var host = await BuildHostAsync(app =>
        {
            app.MapGet("/api/missions/{id:int}/runtime-plan", () => Results.StatusCode((int)HttpStatusCode.NotFound));
        });

        var source = new MissionRuntimeSource(host.GetTestClient(), TestCurrentUser.Default);

        var runtime = await source.GetByIdAsync(404, CancellationToken.None);

        runtime.Should().BeNull();
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
