using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Common.Interfaces;

public interface ITeamBoardBroadcaster
{
    Task BroadcastTeamBoardUpdatedAsync(
        ParticipantTeamBoardDto board,
        CancellationToken cancellationToken);
}
