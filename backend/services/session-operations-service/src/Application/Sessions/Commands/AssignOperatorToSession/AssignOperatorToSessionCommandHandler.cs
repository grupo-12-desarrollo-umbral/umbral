using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

public sealed class AssignOperatorToSessionCommandHandler
    : IRequestHandler<AssignOperatorToSessionCommand, AssignOperatorToSessionResultDto>
{
    private readonly ISessionAdministrationAccessResolver _sessionAdministrationAccessResolver;
    private readonly IAssignableSessionOperatorAccessClient _assignableSessionOperatorAccessClient;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TimeProvider _timeProvider;

    public AssignOperatorToSessionCommandHandler(
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

    public async Task<AssignOperatorToSessionResultDto> Handle(
        AssignOperatorToSessionCommand request,
        CancellationToken cancellationToken)
    {
        var liveSession = await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId,
            cancellationToken);

        var eligibility = await _assignableSessionOperatorAccessClient.GetEligibilityAsync(
            request.OperatorUserId,
            cancellationToken);

        if (!eligibility.IsEligible)
        {
            throw new IneligibleSessionOperatorException(request.OperatorUserId);
        }

        liveSession.AssignOperator(
            request.OperatorUserId,
            eligibility.ExternalIdentityId,
            _timeProvider.GetUtcNow());

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new AssignOperatorToSessionResultDto(
            liveSession.LiveSessionId,
            liveSession.AssignedOperatorUserId ?? request.OperatorUserId);
    }
}
