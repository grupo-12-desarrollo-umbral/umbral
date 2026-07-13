using umbral_backend.Application.Sessions.Commands.ReleaseClue;

namespace umbral_backend.Application.Sessions.Common;

public interface IClueReleaseFacade
{
    Task<ReleaseClueResultDto> ReleaseCluesAsync(
        ReleaseClueCommand command,
        CancellationToken cancellationToken);
}
