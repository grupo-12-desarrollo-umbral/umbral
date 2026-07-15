using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionEventHistoryRepository
{
    Task AppendAsync(SessionEvent sessionEvent, CancellationToken cancellationToken);
}
