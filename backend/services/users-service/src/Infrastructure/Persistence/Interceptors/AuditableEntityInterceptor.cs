using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;

namespace umbral_backend.Infrastructure.Persistence.Interceptors;

public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AuditableEntityInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified) && !entry.HasChangedOwnedEntities())
            {
                continue;
            }

            var now = _timeProvider.GetUtcNow();
            if (entry.State == EntityState.Added)
            {
                entry.Entity.Created = now;
                entry.Entity.CreatedBy = _currentUser.Id;
            }

            entry.Entity.LastModified = now;
            entry.Entity.LastModifiedBy = _currentUser.Id;
        }
    }
}

internal static class EntityEntryExtensions
{
    public static bool HasChangedOwnedEntities(this EntityEntry entry)
    {
        return entry.References.Any(reference =>
            reference.TargetEntry is not null &&
            reference.TargetEntry.Metadata.IsOwned() &&
            reference.TargetEntry.State is EntityState.Added or EntityState.Modified);
    }
}
