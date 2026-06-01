using Microsoft.AspNetCore.Http.HttpResults;
using umbral_backend.Application.Trivias.Commands.CreateTriviaQuiz;
using umbral_backend.Application.Trivias.Commands.UpdateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.DTOs;
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
                    new TriviaQuestionInput(
                        question.Prompt,
                        question.SequenceOrder,
                        question.IsActive,
                        question.Options.Select(option =>
                            new TriviaOptionInput(
                                option.OptionText,
                                option.SequenceOrder,
                                option.IsCorrect))
                            .ToArray()))
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
                    new TriviaQuestionInput(
                        question.Prompt,
                        question.SequenceOrder,
                        question.IsActive,
                        question.Options.Select(option =>
                            new TriviaOptionInput(
                                option.OptionText,
                                option.SequenceOrder,
                                option.IsCorrect))
                            .ToArray()))
                    .ToArray()),
            cancellationToken);

        return TypedResults.Ok(TriviaQuizResponse.FromDto(triviaQuiz));
    }

    public sealed record CreateTriviaQuizRequest(
        string Title,
        string Description,
        IReadOnlyList<TriviaQuestionRequest> Questions);

    public sealed record UpdateTriviaQuizRequest(
        string Title,
        string Description,
        IReadOnlyList<TriviaQuestionRequest> Questions);

    public sealed record TriviaQuestionRequest(
        string Prompt,
        int SequenceOrder,
        bool IsActive,
        IReadOnlyList<TriviaOptionRequest> Options);

    public sealed record TriviaOptionRequest(
        string OptionText,
        int SequenceOrder,
        bool IsCorrect);

    public sealed record TriviaQuizResponse(
        int Id,
        string Title,
        string Description,
        string Status,
        IReadOnlyList<TriviaQuestionResponse> Questions)
    {
        public static TriviaQuizResponse FromDto(TriviaQuizDto triviaQuizDto)
        {
            return new TriviaQuizResponse(
                triviaQuizDto.Id,
                triviaQuizDto.Title,
                triviaQuizDto.Description,
                triviaQuizDto.Status,
                triviaQuizDto.Questions.Select(TriviaQuestionResponse.FromDto).ToList());
        }
    }

    public sealed record TriviaQuestionResponse(
        int Id,
        string Prompt,
        int SequenceOrder,
        bool IsActive,
        IReadOnlyList<TriviaOptionResponse> Options)
    {
        public static TriviaQuestionResponse FromDto(TriviaQuestionDto triviaQuestionDto)
        {
            return new TriviaQuestionResponse(
                triviaQuestionDto.Id,
                triviaQuestionDto.Prompt,
                triviaQuestionDto.SequenceOrder,
                triviaQuestionDto.IsActive,
                triviaQuestionDto.Options.Select(TriviaOptionResponse.FromDto).ToList());
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
        string Status)
    {
        public static TriviaQuizSummaryResponse FromDto(TriviaQuizSummaryDto triviaQuizDto)
        {
            return new TriviaQuizSummaryResponse(
                triviaQuizDto.Id,
                triviaQuizDto.Title,
                triviaQuizDto.Description,
                triviaQuizDto.Status);
        }
    }
}
