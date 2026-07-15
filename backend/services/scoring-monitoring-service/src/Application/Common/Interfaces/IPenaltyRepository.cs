using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IPenaltyRepository
{
    Task AddAsync(Penalty penalty, CancellationToken cancellationToken);
}
