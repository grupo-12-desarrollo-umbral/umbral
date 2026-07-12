using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Common.Interfaces;

public interface IOperatorSessionPanelBroadcaster
{
    Task BroadcastSessionPanelUpdatedAsync(
        OperatorSessionPanelDto panel,
        CancellationToken cancellationToken);
}
