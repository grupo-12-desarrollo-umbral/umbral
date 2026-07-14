using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Sessions.Commands.SubmitTriviaAnswer;

/// <summary>
/// Loads the aggregate and delegates the shared write workflow to EvidenceIntakeFacade. The composed
/// chain runs generic admission before trivia-specific checks, then the aggregate remains the final
/// authority. Accepted answers persist and, only then, raise
/// <c>AnswerRegisteredEvent</c>, which the bridges turn into the RabbitMQ contract and the operator
/// signal. The result carries acceptance metadata only.
/// </summary>
public sealed class SubmitTriviaAnswerCommandHandler
    : IRequestHandler<SubmitTriviaAnswerCommand, SubmitTriviaAnswerResultDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TriviaAnswerValidationChain _validationChain;
    private readonly IEvidenceIntakeFacade _evidenceIntakeFacade;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public SubmitTriviaAnswerCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        TriviaAnswerValidationChain validationChain,
        IEvidenceIntakeFacade evidenceIntakeFacade,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _validationChain = validationChain;
        _evidenceIntakeFacade = evidenceIntakeFacade;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<SubmitTriviaAnswerResultDto> Handle(
        SubmitTriviaAnswerCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _liveSessionRepository.GetByIdAsync(request.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.LiveSessionId);

        // One clock reading shared by the window link and the domain skeleton, so "in time" is
        // evaluated against a single instant on both the fast pre-check and the authoritative write.
        var submittedAt = _timeProvider.GetUtcNow();

        var context = new TriviaAnswerValidationContext(
            session,
            request.TeamId,
            request.TriviaSubstageSnapshotId,
            request.QuestionSequenceOrder,
            request.Token,
            submittedAt);

        var intakeContext = new EvidenceIntakeValidationContext(
            session,
            request.TeamId,
            request.TriviaSubstageSnapshotId,
            request.Token,
            submittedAt);

        var submission = await _evidenceIntakeFacade.RegisterAsync(
            intakeContext,
            ct => _validationChain.ValidateConcreteFormAsync(context, ct),
            liveSession => liveSession.RegisterTriviaAnswer(
                request.TeamId,
                request.SelectedOptionSequenceOrder,
                ResolveParticipantId(liveSession),
                submittedAt),
            cancellationToken);

        return new SubmitTriviaAnswerResultDto(
            session.LiveSessionId,
            submission.TeamId,
            submission.ActiveSubstageId,
            submission.QuestionSequenceOrder,
            submission.SubmittedAt);
    }

    // Attribution guard (HU-34): an accepted trivia answer must always be attributable to a session
    // participant. The RuntimeParticipationLink authorizes by (session, team, token) — never by caller
    // identity — so identity is resolved here, once, to a real participant of THIS session. An absent or
    // unparseable identity claim, or an authenticated non-participant, is rejected before any write; the
    // resolved id is non-nullable so the domain skeleton records a real submitter, never NULL.
    private Guid ResolveParticipantId(LiveSession session)
    {
        if (Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            var participant = session.Participants
                .SingleOrDefault(participant => participant.ExternalIdentityId == externalIdentityId);

            if (participant is not null)
            {
                return participant.SessionParticipantId;
            }
        }

        throw new AnswerSubmitterIsNotSessionParticipantException();
    }
}
