using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Application.Sessions.Common.TargetResolution;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Commands.RegisterTargetScan;

public sealed class RegisterTargetScanCommandHandler
    : IRequestHandler<RegisterTargetScanCommand, RegisterTargetScanResultDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TargetResolutionChain _targetResolutionChain;
    private readonly IEvidenceIntakeFacade _evidenceIntakeFacade;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public RegisterTargetScanCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        TargetResolutionChain targetResolutionChain,
        IEvidenceIntakeFacade evidenceIntakeFacade,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _targetResolutionChain = targetResolutionChain;
        _evidenceIntakeFacade = evidenceIntakeFacade;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<RegisterTargetScanResultDto> Handle(
        RegisterTargetScanCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);
        var submittedAt = _timeProvider.GetUtcNow();
        var activeSubstageId = session.ActiveSubstageId ?? Guid.Empty;
        var intakeContext = new EvidenceIntakeValidationContext(
            session,
            request.TeamId,
            activeSubstageId,
            request.Token,
            submittedAt);
        var resolutionContext = new TargetResolutionContext(
            session,
            request.TeamId,
            activeSubstageId,
            request.ScannedValue);
        TargetResolutionRejectionReason? resolutionRejection = null;

        var submission = await _evidenceIntakeFacade.RegisterAsync(
            intakeContext,
            async ct =>
            {
                resolutionRejection = await _targetResolutionChain.ValidateAsync(resolutionContext, ct);
            },
            liveSession => liveSession.RegisterTargetScan(
                request.TeamId,
                request.ScannedValue,
                ResolveParticipantId(liveSession),
                submittedAt),
            cancellationToken);

        return new RegisterTargetScanResultDto(
            session.LiveSessionId,
            submission.TeamId,
            submission.ActiveSubstageId,
            submission.TargetSnapshotId,
            resolutionRejection is null,
            resolutionRejection?.ToMessage(),
            submission.SubmittedAt);
    }

    private Guid ResolveParticipantId(LiveSession session)
    {
        if (Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            var participant = session.Participants.SingleOrDefault(candidate =>
                candidate.ExternalIdentityId == externalIdentityId);
            if (participant is not null)
            {
                return participant.SessionParticipantId;
            }
        }

        throw new AnswerSubmitterIsNotSessionParticipantException();
    }
}
