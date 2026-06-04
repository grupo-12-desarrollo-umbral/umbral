using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface ITeamReferenceCatalogClient
{
    Task<TeamReferenceDto?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken);
}
