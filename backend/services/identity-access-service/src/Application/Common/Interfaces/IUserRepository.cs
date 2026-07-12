using umbral_backend.Domain.Entities;
using umbral_backend.Application.Common.Models;

namespace umbral_backend.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByExternalIdentityIdAsync(string externalIdentityId, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task<PagedResult<User>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task UpdateAsync(User user, CancellationToken cancellationToken);
}
