using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.DTOs;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class TriviaQuizReadModelRepository : ITriviaQuizReadModelRepository
{
    private readonly ApplicationDbContext _context;

    public TriviaQuizReadModelRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TriviaQuizSummaryDto>> GetTriviaCatalogAsync(CancellationToken cancellationToken)
    {
        return await _context.TriviaQuizzes
            .AsNoTracking()
            .OrderByDescending(triviaQuiz => triviaQuiz.LastModified)
            .Select(triviaQuiz => new TriviaQuizSummaryDto(
                triviaQuiz.Id,
                triviaQuiz.Title,
                triviaQuiz.Description,
                triviaQuiz.Status.ToString(),
                triviaQuiz.SourceTriviaQuizId,
                triviaQuiz.HasUsageHistory,
                triviaQuiz.SourceTriviaQuizId != null))
            .ToListAsync(cancellationToken);
    }

    public async Task<TriviaQuizDto?> GetTriviaDetailAsync(int triviaQuizId, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _context.TriviaQuizzes
            .AsNoTracking()
            .Include(triviaQuiz => triviaQuiz.Questions)
            .ThenInclude(question => question.Options)
            .Where(triviaQuiz => triviaQuiz.Id == triviaQuizId)
            .SingleOrDefaultAsync(cancellationToken);

        if (triviaQuiz is null)
        {
            return null;
        }

        return new TriviaQuizDto(
            triviaQuiz.Id,
            triviaQuiz.Title,
            triviaQuiz.Description,
            triviaQuiz.Status.ToString(),
            triviaQuiz.Questions
                .OrderBy(question => question.SequenceOrder)
                .Select(question => new TriviaQuestionDto(
                    question.Id,
                    question.Prompt,
                    question.SequenceOrder,
                    question.IsActive,
                    question.Options
                        .OrderBy(option => option.SequenceOrder)
                        .Select(option => new TriviaOptionDto(
                            option.Id,
                            option.OptionText,
                            option.SequenceOrder,
                            option.IsCorrect))
                        .ToList(),
                    question.ScoreValue,
                    question.TimeLimit?.Seconds,
                    question.Explanation))
                .ToList(),
            triviaQuiz.SourceTriviaQuizId,
            triviaQuiz.HasUsageHistory,
            triviaQuiz.SourceTriviaQuizId != null);
    }
}
