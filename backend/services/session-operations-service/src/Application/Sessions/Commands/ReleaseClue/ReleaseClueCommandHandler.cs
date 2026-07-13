using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.ReleaseClue;

public sealed class ReleaseClueCommandHandler
    : IRequestHandler<ReleaseClueCommand, ReleaseClueResultDto>
{
    private readonly IClueReleaseFacade _clueReleaseFacade;

    public ReleaseClueCommandHandler(IClueReleaseFacade clueReleaseFacade)
    {
        _clueReleaseFacade = clueReleaseFacade;
    }

    public Task<ReleaseClueResultDto> Handle(
        ReleaseClueCommand request,
        CancellationToken cancellationToken)
    {
        return _clueReleaseFacade.ReleaseCluesAsync(request, cancellationToken);
    }
}
