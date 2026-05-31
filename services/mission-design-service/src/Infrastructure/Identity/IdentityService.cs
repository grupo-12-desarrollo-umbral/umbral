using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Models;

namespace umbral_backend.Infrastructure.Identity;

internal sealed class IdentityService : IIdentityService
{
    public Task<string?> GetUserNameAsync(string userId)
    {
        return Task.FromResult<string?>(userId);
    }

    public Task<bool> IsInRoleAsync(string userId, string role)
    {
        return Task.FromResult(false);
    }

    public Task<bool> AuthorizeAsync(string userId, string policyName)
    {
        return Task.FromResult(false);
    }

    public Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password)
    {
        return Task.FromResult((Result.Failure(["Identity is not implemented in mission-design-service."]), string.Empty));
    }

    public Task<Result> DeleteUserAsync(string userId)
    {
        return Task.FromResult(Result.Failure(["Identity is not implemented in mission-design-service."]));
    }
}
