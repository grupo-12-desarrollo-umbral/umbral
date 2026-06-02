using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Web.Endpoints;

namespace umbral_backend.Web.UnitTests.Endpoints;

public class TriviaQuizResponseTests
{
    [Fact]
    public void FromDto_MapsQuizBasicsAndAssociatedQuestions()
    {
        var dto = new TriviaQuizDto(
            10,
            "Trivia Capitals",
            "Capital cities quiz.",
            "Draft",
            [
                new TriviaQuestionDto(
                    21,
                    "Capital of France?",
                    1,
                    true,
                    [
                        new TriviaOptionDto(31, "Paris", 1, true),
                        new TriviaOptionDto(32, "Berlin", 2, false)
                    ],
                    100,
                    20,
                    "Paris is the capital city.")
            ]);

        var response = TriviasEndpoints.TriviaQuizResponse.FromDto(dto);

        response.Id.Should().Be(10);
        response.Title.Should().Be("Trivia Capitals");
        response.Status.Should().Be("Draft");
        response.Questions.Should().ContainSingle();
        response.Questions[0].Prompt.Should().Be("Capital of France?");
        response.Questions[0].Options.Should().HaveCount(2);
        response.Questions[0].Options[0].OptionText.Should().Be("Paris");
        response.Questions[0].Options[0].IsCorrect.Should().BeTrue();
        response.Questions[0].ScoreValue.Should().Be(100);
        response.Questions[0].TimeLimitSeconds.Should().Be(20);
        response.Questions[0].Explanation.Should().Be("Paris is the capital city.");
    }
}

public class TriviaQuizSummaryResponseTests
{
    [Fact]
    public void FromDto_MapsSummaryFields()
    {
        var dto = new TriviaQuizSummaryDto(12, "Science", "Basic science trivia.", "Draft");

        var response = TriviasEndpoints.TriviaQuizSummaryResponse.FromDto(dto);

        response.Id.Should().Be(12);
        response.Title.Should().Be("Science");
        response.Description.Should().Be("Basic science trivia.");
        response.Status.Should().Be("Draft");
    }
}
