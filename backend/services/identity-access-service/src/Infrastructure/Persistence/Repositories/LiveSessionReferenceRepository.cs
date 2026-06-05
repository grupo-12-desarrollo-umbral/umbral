using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class LiveSessionReferenceRepository : ILiveSessionReferenceRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public LiveSessionReferenceRepository(ApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public Task<LiveSessionReference?> GetByIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return _context.LiveSessionReferences
            .Include(liveSessionReference => liveSessionReference.TeamAssociations)
            .SingleOrDefaultAsync(
                liveSessionReference => liveSessionReference.LiveSessionId == liveSessionId,
                cancellationToken);
    }

    public Task<LiveSessionReference?> GetBySessionCodeAsync(string sessionCode, CancellationToken cancellationToken)
    {
        return _context.LiveSessionReferences
            .SingleOrDefaultAsync(
                liveSessionReference => liveSessionReference.SessionCode == sessionCode,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SessionTeamLobbyEntry>> ListTeamLobbyEntriesAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            return [];
        }

        var actor = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.ExternalIdentityId == _currentUser.Id, cancellationToken);

        if (actor is null)
        {
            return [];
        }

        return await (
            from association in _context.SessionTeamAssociations.AsNoTracking()
            join team in _context.Teams.AsNoTracking() on association.TeamId equals team.TeamId
            where association.LiveSessionId == liveSessionId && team.IsActive
            join membership in _context.TeamMemberships.AsNoTracking().Where(membership => membership.UserId == actor.Id)
                on team.TeamId equals membership.TeamId into membershipGroup
            from membership in membershipGroup.DefaultIfEmpty()
            orderby team.DisplayName, team.TeamId
            select new SessionTeamLobbyEntry(
                team.TeamId,
                team.DisplayName,
                membership != null))
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> IsTeamAssociatedAsync(Guid liveSessionId, Guid teamId, CancellationToken cancellationToken)
    {
        return _context.SessionTeamAssociations
            .AsNoTracking()
            .AnyAsync(
                association => association.LiveSessionId == liveSessionId && association.TeamId == teamId,
                cancellationToken);
    }

    public Task<ParticipantSessionMembershipLookup?> GetParticipantMembershipAsync(
        Guid liveSessionId,
        int userId,
        CancellationToken cancellationToken)
    {
        return (
            from association in _context.SessionTeamAssociations.AsNoTracking()
            join membership in _context.TeamMemberships.AsNoTracking()
                on association.TeamId equals membership.TeamId
            where association.LiveSessionId == liveSessionId && membership.UserId == userId
            orderby membership.AssignedAt, membership.TeamMembershipId
            select new ParticipantSessionMembershipLookup(
                membership.TeamId,
                membership.TeamMembershipId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(LiveSessionReference liveSessionReference, CancellationToken cancellationToken)
    {
        await _context.LiveSessionReferences.AddAsync(liveSessionReference, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(LiveSessionReference liveSessionReference, CancellationToken cancellationToken)
    {
        _context.LiveSessionReferences.Update(liveSessionReference);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
