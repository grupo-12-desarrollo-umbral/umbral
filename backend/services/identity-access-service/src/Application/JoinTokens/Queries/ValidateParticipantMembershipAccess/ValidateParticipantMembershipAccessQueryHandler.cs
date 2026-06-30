using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

// Real subject wrapped by ParticipantMembershipAccessAuthorizationProxy (the registered IRequestHandler).
public sealed class ValidateParticipantMembershipAccessQueryHandler
{
    private readonly IJoinTokenRepository _joinTokenRepository;
    private readonly IJoinTokenTokenService _joinTokenTokenService;
    private readonly JoinTokenPolicy _joinTokenPolicy;
    private readonly TimeProvider _timeProvider;

    public ValidateParticipantMembershipAccessQueryHandler(
        IJoinTokenRepository joinTokenRepository,
        IJoinTokenTokenService joinTokenTokenService,
        JoinTokenPolicy joinTokenPolicy,
        TimeProvider timeProvider)
    {
        _joinTokenRepository = joinTokenRepository;
        _joinTokenTokenService = joinTokenTokenService;
        _joinTokenPolicy = joinTokenPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<ParticipantMembershipAccessDecisionDto> Handle(
        ValidateParticipantMembershipAccessQuery query,
        CancellationToken cancellationToken)
    {
        var decision = await BuildDecisionAsync(query, cancellationToken);

        return new ParticipantMembershipAccessDecisionDto(
            decision.Capability.ToString(),
            decision.IsAllowed,
            decision.Reason,
            query.LiveSessionId,
            query.TeamId);
    }

    private async Task<AccessDecision> BuildDecisionAsync(
        ValidateParticipantMembershipAccessQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Token))
        {
            return AccessDecision.Allow(
                ProtectedCapability.ParticipantExperience,
                "Participant membership validated.");
        }

        var tokenHash = _joinTokenTokenService.HashToken(query.Token);
        var joinToken = await _joinTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (joinToken is null)
        {
            return AccessDecision.Deny(
                ProtectedCapability.ParticipantExperience,
                "Join token is invalid.");
        }

        if (joinToken.LiveSessionId != query.LiveSessionId || joinToken.TeamId != query.TeamId)
        {
            return AccessDecision.Deny(
                ProtectedCapability.ParticipantExperience,
                "Join token does not match the requested session/team context.");
        }

        try
        {
            _joinTokenPolicy.EnsureIsValid(joinToken, _timeProvider.GetUtcNow());
        }
        catch (JoinTokenExpiredException exception)
        {
            return AccessDecision.Deny(ProtectedCapability.ParticipantExperience, exception.Message);
        }
        catch (JoinTokenReplayRejectedException exception)
        {
            return AccessDecision.Deny(ProtectedCapability.ParticipantExperience, exception.Message);
        }

        return AccessDecision.Allow(
            ProtectedCapability.ParticipantExperience,
            "Participant membership and join token validated.");
    }
}
