using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

public sealed class AssignOperatorToSessionFacade : IAssignOperatorToSessionFacade
{
    private readonly ISessionAdministrationAccessResolver _sessionAdministrationAccessResolver;
    private readonly IAssignableSessionOperatorAccessClient _assignableSessionOperatorAccessClient;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TimeProvider _timeProvider;

    public AssignOperatorToSessionFacade(
        ISessionAdministrationAccessResolver sessionAdministrationAccessResolver,
        IAssignableSessionOperatorAccessClient assignableSessionOperatorAccessClient,
        ILiveSessionRepository liveSessionRepository,
        TimeProvider timeProvider)
    {
        _sessionAdministrationAccessResolver = sessionAdministrationAccessResolver;
        _assignableSessionOperatorAccessClient = assignableSessionOperatorAccessClient;
        _liveSessionRepository = liveSessionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AssignOperatorToSessionResultDto> AssignAsync(
        AssignOperatorToSessionCommand command,
        CancellationToken cancellationToken)
    {
        var liveSession = await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(
            command.LiveSessionId,
            cancellationToken);

        var eligibility = await _assignableSessionOperatorAccessClient.GetEligibilityAsync(
            command.OperatorUserId,
            cancellationToken);

        if (!eligibility.IsEligible)
        {
            throw new IneligibleSessionOperatorException(command.OperatorUserId);
        }

        liveSession.AssignOperator(command.OperatorUserId, _timeProvider.GetUtcNow());

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new AssignOperatorToSessionResultDto(
            liveSession.LiveSessionId,
            liveSession.AssignedOperatorUserId ?? command.OperatorUserId);
    }
}
