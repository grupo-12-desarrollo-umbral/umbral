using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Scores.Common.Authorization;

public interface IScoringSessionAccessResolver
{
    Task EnsureAccessAsync(Guid liveSessionId, CancellationToken cancellationToken);
}
