using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Application.Sessions.Common.EvidenceValidation;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Shared evidence-intake Facade. It owns the stable transaction-facing workflow for every concrete
/// form: generic admission chain, form-specific extension checks, aggregate registration, then one
/// repository update. The aggregate-raised registration fact is captured by the existing pre-commit
/// transactional outbox dispatcher during that update.
/// </summary>
public sealed class EvidenceIntakeFacade : IEvidenceIntakeFacade
{
    private readonly EvidenceIntakeValidationChain _validationChain;
    private readonly EvidenceValidationChain _contextualValidationChain;
    private readonly ILiveSessionRepository _liveSessionRepository;

    public EvidenceIntakeFacade(
        EvidenceIntakeValidationChain validationChain,
        ILiveSessionRepository liveSessionRepository)
        : this(validationChain, new EvidenceValidationChain([]), liveSessionRepository)
    {
    }

    public EvidenceIntakeFacade(
        EvidenceIntakeValidationChain validationChain,
        EvidenceValidationChain contextualValidationChain,
        ILiveSessionRepository liveSessionRepository)
    {
        _validationChain = validationChain;
        _contextualValidationChain = contextualValidationChain;
        _liveSessionRepository = liveSessionRepository;
    }

    public async Task<TSubmission> RegisterAsync<TSubmission>(
        EvidenceIntakeValidationContext context,
        Func<CancellationToken, Task> validateConcreteForm,
        Func<LiveSession, TSubmission> registerConcreteForm,
        CancellationToken cancellationToken)
        where TSubmission : EvidenceSubmission
    {
        await _validationChain.ValidateAsync(context, cancellationToken);
        await validateConcreteForm(cancellationToken);

        // The concrete aggregate entry point invokes LiveSession.RegisterEvidenceCore, which remains
        // the final authority and raises EvidenceSubmissionRegisteredEvent on success only.
        var submission = registerConcreteForm(context.Session);

        await _liveSessionRepository.UpdateAsync(context.Session, cancellationToken);
        return submission;
    }

    public async Task<TSubmission> RegisterPendingAsync<TSubmission>(
        EvidenceIntakeValidationContext context,
        string? origin,
        Func<LiveSession, TSubmission> registerPending,
        Func<TSubmission, CancellationToken, Task> validateConcreteForm,
        Func<TSubmission, CancellationToken, Task> acceptConcreteForm,
        CancellationToken cancellationToken)
        where TSubmission : EvidenceSubmission
    {
        await _validationChain.ValidateAsync(context, cancellationToken);

        var submission = registerPending(context.Session);
        var contextualValidationContext = new EvidenceValidationContext(
            submission,
            context.Session,
            context.TeamId,
            context.ActiveSubstageId,
            origin,
            context.SubmittedAt);

        try
        {
            await _contextualValidationChain.ValidateAsync(
                contextualValidationContext,
                cancellationToken);
        }
        catch (EvidenceContextRejectedException exception)
        {
            submission.Reject(exception.Reason);
            await _liveSessionRepository.UpdateAsync(context.Session, cancellationToken);
            return submission;
        }

        await validateConcreteForm(submission, cancellationToken);
        await acceptConcreteForm(submission, cancellationToken);
        await _liveSessionRepository.UpdateAsync(context.Session, cancellationToken);
        return submission;
    }
}
