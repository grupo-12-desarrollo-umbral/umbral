using Microsoft.AspNetCore.Http.HttpResults;
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
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Application.Trivias.Queries.GetTriviaDetail;

namespace umbral_backend.Web.Endpoints;

public sealed class TriviasEndpoints : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var trivias = groupBuilder.MapGroup("/api/trivias");

        trivias.MapPost("/", CreateTriviaQuiz);
        trivias.MapGet("/", GetTriviaCatalog);
        trivias.MapGet("/{id:int}", GetTriviaDetail);
        trivias.MapPut("/{id:int}", UpdateTriviaQuiz);
        trivias.MapDelete("/{id:int}", DeleteTriviaQuiz);
        trivias.MapPost("/{id:int}/duplicate", DuplicateTriviaQuiz);
        trivias.MapPost("/{id:int}/publish", PublishTriviaQuiz);
        trivias.MapPost("/{id:int}/archive", ArchiveTriviaQuiz);
        trivias.MapPost("/{id:int}/retire", RetireTriviaQuiz);
        trivias.MapPost("/{triviaQuizId:int}/questions", AddTriviaQuestion);
        trivias.MapPut("/{triviaQuizId:int}/questions/{questionId:int}", UpdateTriviaQuestion);
    }

    private static async Task<Created<TriviaQuizResponse>> CreateTriviaQuiz(
        ISender sender,
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

        return TypedResults.Created($"/api/trivias/{triviaQuiz.Id}", TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<Ok<IReadOnlyList<TriviaQuizSummaryResponse>>> GetTriviaCatalog(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var triviaCatalog = await sender.Send(new GetTriviaCatalogQuery(), cancellationToken);
        IReadOnlyList<TriviaQuizSummaryResponse> response = triviaCatalog
            .Select(TriviaQuizSummaryResponse.FromDto)
            .ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<TriviaQuizResponse>> GetTriviaDetail(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new GetTriviaDetailQuery(id), cancellationToken);
        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<Ok<TriviaQuizResponse>> UpdateTriviaQuiz(
        ISender sender,
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

        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<NoContent> DeleteTriviaQuiz(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTriviaQuizCommand(id), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Created<TriviaQuizResponse>> DuplicateTriviaQuiz(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new DuplicateTriviaQuizCommand(id), cancellationToken);
        return TypedResults.Created($"/api/trivias/{triviaQuiz.Id}", TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<Ok<TriviaQuizResponse>> PublishTriviaQuiz(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new PublishTriviaQuizCommand(id), cancellationToken);
        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<Ok<TriviaQuizResponse>> ArchiveTriviaQuiz(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new ArchiveTriviaQuizCommand(id), cancellationToken);
        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<Ok<TriviaQuizResponse>> RetireTriviaQuiz(
        ISender sender,
        int id,
        CancellationToken cancellationToken)
    {
        var triviaQuiz = await sender.Send(new RetireTriviaQuizCommand(id), cancellationToken);
        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<Ok<TriviaQuizResponse>> AddTriviaQuestion(
        ISender sender,
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

        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    private static async Task<Ok<TriviaQuizResponse>> UpdateTriviaQuestion(
        ISender sender,
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

        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
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
