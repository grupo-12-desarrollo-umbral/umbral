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

    public Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken)
    {
        return _context.Teams
            .SingleOrDefaultAsync(team => team.TeamId == teamId, cancellationToken);
    }

    public Task<Team?> GetByIdWithMembershipsAsync(Guid teamId, CancellationToken cancellationToken)
    {
        return _context.Teams
            .Include(team => team.Memberships)
            .SingleOrDefaultAsync(team => team.TeamId == teamId, cancellationToken);
    }

    public async Task<PagedResult<Team>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.Teams
            .AsNoTracking()
            .OrderBy(team => team.DisplayName)
            .ThenBy(team => team.TeamId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<Team>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<bool> TeamCodeExistsAsync(string teamCode, Guid? excludeTeamId, CancellationToken cancellationToken)
    {
        return _context.Teams.AnyAsync(
            team => team.TeamCode == teamCode && (!excludeTeamId.HasValue || team.TeamId != excludeTeamId.Value),
            cancellationToken);
    }

    public async Task AddAsync(Team team, CancellationToken cancellationToken)
    {
        await _context.Teams.AddAsync(team, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken)
    {
        _context.Teams.Update(team);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
