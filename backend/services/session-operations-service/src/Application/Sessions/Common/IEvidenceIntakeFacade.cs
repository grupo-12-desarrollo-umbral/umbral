using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

public interface IEvidenceIntakeFacade
{
    Task<TSubmission> RegisterAsync<TSubmission>(
        EvidenceIntakeValidationContext context,
        Func<CancellationToken, Task> validateConcreteForm,
        Func<LiveSession, TSubmission> registerConcreteForm,
        CancellationToken cancellationToken)
        where TSubmission : EvidenceSubmission;
}
