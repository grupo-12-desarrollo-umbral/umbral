using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByExternalIdentityIdAsync(string externalIdentityId, CancellationToken cancellationToken)
    {
        return _context.Users
            .Include(user => user.IdentityProviderSessions)
            .SingleOrDefaultAsync(
                user => user.ExternalIdentityId == externalIdentityId,
                cancellationToken);
    }

    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken)
    {
        return _context.Users
            .Include(user => user.IdentityProviderSessions)
            .SingleOrDefaultAsync(
                user => user.Id == userId,
                cancellationToken);
    }

    public async Task<Application.Common.Models.PagedResult<User>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _context.Users
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new Application.Common.Models.PagedResult<User>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
