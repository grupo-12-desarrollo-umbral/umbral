using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class TeamRepository : ITeamRepository
{
    private readonly ApplicationDbContext _context;

    public TeamRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<RegisteredTeam?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken)
    {
        return _context.RegisteredTeams
            .SingleOrDefaultAsync(team => team.TeamId == teamId, cancellationToken);
    }

    public Task<RegisteredTeam?> GetByIdWithMembershipsAsync(Guid teamId, CancellationToken cancellationToken)
    {
        return _context.RegisteredTeams
            .Include(team => team.Memberships)
            .SingleOrDefaultAsync(team => team.TeamId == teamId, cancellationToken);
    }

    public async Task<IReadOnlyList<RegisteredTeam>> ListActiveByParticipantAsync(int userId, CancellationToken cancellationToken)
    {
        return await _context.RegisteredTeams
            .AsNoTracking()
            .Where(team => team.IsActive && team.Memberships.Any(membership => membership.UserId == userId))
            .OrderBy(team => team.DisplayName)
            .ThenBy(team => team.TeamId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PagedResult<RegisteredTeam>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.RegisteredTeams
            .AsNoTracking()
            .OrderBy(team => team.DisplayName)
            .ThenBy(team => team.TeamId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<RegisteredTeam>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<bool> TeamCodeExistsAsync(string teamCode, Guid? excludeTeamId, CancellationToken cancellationToken)
    {
        return _context.RegisteredTeams.AnyAsync(
            team => team.TeamCode == teamCode && (!excludeTeamId.HasValue || team.TeamId != excludeTeamId.Value),
            cancellationToken);
    }

    public async Task AddAsync(RegisteredTeam team, CancellationToken cancellationToken)
    {
        await _context.RegisteredTeams.AddAsync(team, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RegisteredTeam team, CancellationToken cancellationToken)
    {
        _context.RegisteredTeams.Update(team);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
