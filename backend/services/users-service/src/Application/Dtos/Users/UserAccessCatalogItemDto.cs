namespace umbral_backend.Application.Dtos.Users;

public sealed record UserAccessCatalogItemDto(
    int Id,
    string ExternalIdentityId,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive);
