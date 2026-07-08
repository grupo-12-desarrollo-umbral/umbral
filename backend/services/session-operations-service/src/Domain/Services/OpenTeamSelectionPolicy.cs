using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

// Open Team Selection pre-start policy (issue #88; decisions doc §7/§9). Decides which attached
// teams a participant may self-assign into before the session starts. It gates on the participant's
// authorized set (their RegisteredTeamMembership whitelist from Users, passed in as reference-team
// ids); it does NOT check capacity/freeze/switching — that is #89. Callers must have already
// confirmed the participant is eligible (Users IsEligible); an empty authorized set here means
// "unassigned → Open Team Selection", never "denied".
public sealed class OpenTeamSelectionPolicy
{
    // Teams the participant may pick, given the teams they are whitelisted for. Empty while the
    // session is past pre-start. An "unassigned" participant — one with no whitelisted team among
    // the session's attached teams (decisions §7) — gets every attached team (Open Team Selection).
    public IReadOnlyCollection<Team> SelectableTeams(LiveSession session, IReadOnlySet<Guid> authorizedReferenceTeamIds)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(authorizedReferenceTeamIds);

        if (!IsOpenForSelection(session))
        {
            return Array.Empty<Team>();
        }

        var whitelisted = session.Teams
            .Where(team => team.ReferenceTeamId is Guid referenceTeamId && authorizedReferenceTeamIds.Contains(referenceTeamId))
            .ToList();

        // No whitelisted attached team => unassigned => Open Team Selection over all attached teams.
        return whitelisted.Count > 0 ? whitelisted : session.Teams.ToList();
    }

    public void EnsureCanSelfAssign(LiveSession session, Team targetTeam, IReadOnlySet<Guid> authorizedReferenceTeamIds)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(targetTeam);
        ArgumentNullException.ThrowIfNull(authorizedReferenceTeamIds);

        if (!IsOpenForSelection(session))
        {
            throw new OpenTeamSelectionClosedException(session.State);
        }

        var selectable = SelectableTeams(session, authorizedReferenceTeamIds);
        if (!selectable.Any(team => team.TeamId == targetTeam.TeamId))
        {
            throw new TeamNotInAuthorizedSetException(targetTeam.TeamId);
        }
    }

    private static bool IsOpenForSelection(LiveSession session)
    {
        return session.State is SessionState.Scheduled or SessionState.Preparing;
    }
}
