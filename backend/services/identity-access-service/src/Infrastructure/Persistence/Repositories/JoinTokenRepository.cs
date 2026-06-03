using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class JoinTokenRepository : IJoinTokenRepository
{
    private readonly ApplicationDbContext _context;

    public JoinTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<JoinToken?> GetByIdAsync(Guid joinTokenId, CancellationToken cancellationToken)
    {
        return _context.JoinTokens
            .SingleOrDefaultAsync(joinToken => joinToken.JoinTokenId == joinTokenId, cancellationToken);
    }

    public Task<JoinToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return _context.JoinTokens
            .SingleOrDefaultAsync(joinToken => joinToken.TokenHash == tokenHash, cancellationToken);
    }

    public Task<JoinToken?> GetByLiveSessionIdAndTeamIdAsync(
        Guid liveSessionId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        return _context.JoinTokens
            .OrderByDescending(joinToken => joinToken.Status == JoinTokenStatus.Active)
            .ThenByDescending(joinToken => joinToken.IssuedAt)
            .FirstOrDefaultAsync(
                joinToken => joinToken.LiveSessionId == liveSessionId && joinToken.TeamId == teamId,
                cancellationToken);
    }

    public async Task AddAsync(JoinToken joinToken, CancellationToken cancellationToken)
    {
        await _context.JoinTokens.AddAsync(joinToken, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
