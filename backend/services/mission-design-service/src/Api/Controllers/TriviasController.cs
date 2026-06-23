using Microsoft.AspNetCore.Mvc;
using umbral_backend.Application.Trivias.Commands.AddTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DeleteTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuestion;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;

namespace umbral_backend.Web.Controllers;

[ApiController]
[Route("api/trivias")]
public sealed class TriviasController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TriviaQuizResponse>> CreateTriviaQuiz(
        CreateTriviaQuizRequest request,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(
            new CreateTriviaQuizCommand(
                request.Title,
                request.Description,
                request.Questions.Select(question =>
                    MapTriviaQuestionInput(
                        question.Prompt,
                        question.SequenceOrder,
                        question.IsActive,
                        question.Options,
                        question.ScoreValue,
                        question.TimeLimitSeconds,
                        question.Explanation))
                    .ToArray()),
            cancellationToken);

        return Created($"/api/trivias/{triviaQuiz.Id}", TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TriviaQuizSummaryResponse>>> GetTriviaCatalog(
        CancellationToken cancellationToken)
    {
        var triviaCatalog = await sender.Send(new GetTriviaCatalogQuery(), cancellationToken);
        IReadOnlyList<TriviaQuizSummaryResponse> response = triviaCatalog
            .Select(TriviaQuizSummaryResponse.FromDto)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TriviaQuizResponse>> GetTriviaDetail(
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new GetTriviaDetailQuery(id), cancellationToken);
        return Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TriviaQuizResponse>> UpdateTriviaQuiz(
        int id,
        UpdateTriviaQuizRequest request,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(
            new UpdateTriviaQuizCommand(
                id,
                request.Title,
                request.Description,
                request.Questions.Select(question =>
                    MapTriviaQuestionInput(
                        question.Prompt,
                        question.SequenceOrder,
                        question.IsActive,
                        question.Options,
                        question.ScoreValue,
                        question.TimeLimitSeconds,
                        question.Explanation))
                    .ToArray()),
            cancellationToken);

        return Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTriviaQuiz(
        int id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTriviaQuizCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/duplicate")]
    public async Task<ActionResult<TriviaQuizResponse>> DuplicateTriviaQuiz(
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new DuplicateTriviaQuizCommand(id), cancellationToken);
        return Created($"/api/trivias/{triviaQuiz.Id}", TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<TriviaQuizResponse>> PublishTriviaQuiz(
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new PublishTriviaQuizCommand(id), cancellationToken);
        return Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpPost("{id:int}/archive")]
    public async Task<ActionResult<TriviaQuizResponse>> ArchiveTriviaQuiz(
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new ArchiveTriviaQuizCommand(id), cancellationToken);
        return Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpPost("{id:int}/retire")]
    public async Task<ActionResult<TriviaQuizResponse>> RetireTriviaQuiz(
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new RetireTriviaQuizCommand(id), cancellationToken);
        return Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpPost("{triviaQuizId:int}/questions")]
    public async Task<ActionResult<TriviaQuizResponse>> AddTriviaQuestion(
        int triviaQuizId,
        AddTriviaQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(
            new AddTriviaQuestionCommand(
                triviaQuizId,
                request.Prompt,
                request.SequenceOrder,
                request.ScoreValue,
                request.TimeLimitSeconds,
                request.Explanation,
                request.IsActive,
                request.Options.Select(MapTriviaOptionInput).ToArray()),
            cancellationToken);

        return Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    [HttpPut("{triviaQuizId:int}/questions/{questionId:int}")]
    public async Task<ActionResult<TriviaQuizResponse>> UpdateTriviaQuestion(
        int triviaQuizId,
        int questionId,
        UpdateTriviaQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(
            new UpdateTriviaQuestionCommand(
                triviaQuizId,
                questionId,
                request.Prompt,
                request.SequenceOrder,
                request.ScoreValue,
                request.TimeLimitSeconds,
                request.Explanation,
                request.IsActive,
                request.Options.Select(MapTriviaOptionInput).ToArray()),
            cancellationToken);

        return Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static TriviaQuestionInput MapTriviaQuestionInput(
        string prompt,
        int sequenceOrder,
        bool isActive,
        IReadOnlyList<TriviaOptionRequest> options,
        int? scoreValue,
        int? timeLimitSeconds,
        string? explanation)
    {
        return new TriviaQuestionInput(
            prompt,
            sequenceOrder,
            isActive,
            options.Select(MapTriviaOptionInput).ToArray(),
            scoreValue,
            timeLimitSeconds,
            explanation);
    }

    private static TriviaOptionInput MapTriviaOptionInput(TriviaOptionRequest option)
    {
        return new TriviaOptionInput(
            option.OptionText,
            option.SequenceOrder,
            option.IsCorrect);
    }

    public sealed record CreateTriviaQuizRequest(
        string Title,
        string Description,
        IReadOnlyList<TriviaQuestionRequest> Questions);

    public sealed record UpdateTriviaQuizRequest(
        string Title,
        string Description,
        IReadOnlyList<TriviaQuestionRequest> Questions);

    public sealed record AddTriviaQuestionRequest(
        string Prompt,
        int SequenceOrder,
        int ScoreValue,
        int TimeLimitSeconds,
        string? Explanation,
        bool IsActive,
        IReadOnlyList<TriviaOptionRequest> Options);

    public sealed record UpdateTriviaQuestionRequest(
        string Prompt,
        int SequenceOrder,
        int ScoreValue,
        int TimeLimitSeconds,
        string? Explanation,
        bool IsActive,
        IReadOnlyList<TriviaOptionRequest> Options);

    public sealed record TriviaQuestionRequest(
        string Prompt,
        int SequenceOrder,
        bool IsActive,
        IReadOnlyList<TriviaOptionRequest> Options,
        int? ScoreValue = null,
        int? TimeLimitSeconds = null,
        string? Explanation = null);

    public sealed record TriviaOptionRequest(
        string OptionText,
        int SequenceOrder,
        bool IsCorrect);

    public sealed record TriviaQuizResponse(
        int Id,
        string Title,
        string Description,
        string Status,
        bool IsSourceReady,
        int? SourceTriviaQuizId,
        bool HasUsageHistory,
        bool IsDuplicate,
        IReadOnlyList<TriviaQuestionResponse> Questions)
    {
        public static TriviaQuizResponse FromDto(TriviaQuizDto triviaQuizDto)
        {
            return new TriviaQuizResponse(
                triviaQuizDto.Id,
                triviaQuizDto.Title,
                triviaQuizDto.Description,
                triviaQuizDto.Status,
                string.Equals(triviaQuizDto.Status, "Published", StringComparison.Ordinal),
                triviaQuizDto.SourceTriviaQuizId,
                triviaQuizDto.HasUsageHistory,
                triviaQuizDto.IsDuplicate,
                triviaQuizDto.Questions.Select(TriviaQuestionResponse.FromDto).ToList());
        }
    }

    public sealed record TriviaQuestionResponse(
        int Id,
        string Prompt,
        int SequenceOrder,
        bool IsActive,
        IReadOnlyList<TriviaOptionResponse> Options,
        int? ScoreValue,
        int? TimeLimitSeconds,
        string? Explanation)
    {
        public static TriviaQuestionResponse FromDto(TriviaQuestionDto triviaQuestionDto)
        {
            return new TriviaQuestionResponse(
                triviaQuestionDto.Id,
                triviaQuestionDto.Prompt,
                triviaQuestionDto.SequenceOrder,
                triviaQuestionDto.IsActive,
                triviaQuestionDto.Options.Select(TriviaOptionResponse.FromDto).ToList(),
                triviaQuestionDto.ScoreValue,
                triviaQuestionDto.TimeLimitSeconds,
                triviaQuestionDto.Explanation);
        }
    }

    public sealed record TriviaOptionResponse(
        int Id,
        string OptionText,
        int SequenceOrder,
        bool IsCorrect)
    {
        public static TriviaOptionResponse FromDto(TriviaOptionDto triviaOptionDto)
        {
            return new TriviaOptionResponse(
                triviaOptionDto.Id,
                triviaOptionDto.OptionText,
                triviaOptionDto.SequenceOrder,
                triviaOptionDto.IsCorrect);
        }
    }

    public sealed record TriviaQuizSummaryResponse(
        int Id,
        string Title,
        string Description,
        string Status,
        bool IsSourceReady,
        int? SourceTriviaQuizId,
        bool HasUsageHistory,
        bool IsDuplicate)
    {
        public static TriviaQuizSummaryResponse FromDto(TriviaQuizSummaryDto triviaQuizDto)
        {
            return new TriviaQuizSummaryResponse(
                triviaQuizDto.Id,
                triviaQuizDto.Title,
                triviaQuizDto.Description,
                triviaQuizDto.Status,
                string.Equals(triviaQuizDto.Status, "Published", StringComparison.Ordinal),
                triviaQuizDto.SourceTriviaQuizId,
                triviaQuizDto.HasUsageHistory,
                triviaQuizDto.IsDuplicate);
        }
    }
}
