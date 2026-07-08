namespace umbral_backend.Application.Common.Interfaces;

// Pushes a Participation Block (#91) to the blocked participant's live connection so a connected
// client is evicted at the moment of the denied re-check, not only on its next server call.
public interface IParticipantBlockNotifier
{
    Task NotifyBlockedAsync(Guid sessionParticipantId, CancellationToken cancellationToken);
}
