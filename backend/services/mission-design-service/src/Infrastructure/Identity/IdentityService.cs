using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Identity;

internal sealed class IdentityService : IIdentityService
{
    public Task<string?> GetUserNameAsync(string userId)
    {
        return Task.FromResult<string?>(userId);
    }
}
