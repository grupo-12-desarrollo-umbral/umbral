using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class TriviaQuizRepository : ITriviaQuizRepository
{
    private readonly ApplicationDbContext _context;

    public TriviaQuizRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<TriviaQuiz?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken)
    {
        return _context.TriviaQuizzes
            .Include(triviaQuiz => triviaQuiz.Questions)
            .ThenInclude(question => question.Options)
            .SingleOrDefaultAsync(triviaQuiz => triviaQuiz.Id == triviaQuizId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, TriviaQuizStatus>> GetStatusesByIdsAsync(
        IReadOnlyCollection<int> triviaQuizIds,
        CancellationToken cancellationToken)
    {
        if (triviaQuizIds.Count == 0)
        {
            return new Dictionary<int, TriviaQuizStatus>();
        }

        var statuses = await _context.TriviaQuizzes
            .Where(triviaQuiz => triviaQuizIds.Contains(triviaQuiz.Id))
            .Select(triviaQuiz => new { triviaQuiz.Id, triviaQuiz.Status })
            .ToListAsync(cancellationToken);

        return statuses.ToDictionary(entry => entry.Id, entry => entry.Status);
    }

    public async Task AddAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        await _context.TriviaQuizzes.AddAsync(triviaQuiz, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        _context.TriviaQuizzes.Update(triviaQuiz);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        _context.TriviaQuizzes.Remove(triviaQuiz);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
