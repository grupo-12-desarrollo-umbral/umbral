using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;

namespace umbral_backend.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Two-phase domain-event dispatcher.
/// <para>
/// <b>Pre-commit (SavingChanges):</b> the transactional-outbox integration publishers run via
/// <see cref="IOutboxDomainEventDispatcher"/>, so each <c>IPublishEndpoint.Publish</c> — a local
/// OutboxMessage insert under the bus outbox — is flushed by the same SaveChanges/transaction as the
/// business write. The event is captured atomically and drained to RabbitMQ asynchronously: no 5s
/// broker stall on the hot path, no silent loss on a broker outage.
/// </para>
/// <para>
/// <b>Post-commit (SavedChanges):</b> every other notification handler (SignalR broadcasts,
/// orchestration) runs on the MediatR path, after the transaction commits, where re-loading state and
/// re-entrant saves are safe. The events are snapshotted and cleared from the entities <b>before</b>
/// the MediatR fan-out, so a re-entrant SaveChanges triggered by a handler (e.g. activating the next
/// question) sees no stale events and cannot re-dispatch or re-enqueue them.
/// </para>
/// </summary>
public sealed class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;

    // The outbox dispatcher is resolved lazily (see DispatchOutboxAsync), so it is NOT a constructor
    // dependency: it transitively needs the bus-outbox IPublishEndpoint, which depends on the
    // ApplicationDbContext this interceptor is attached to. Injecting it here would close a DI cycle
    // (DbContext -> interceptor -> IPublishEndpoint -> DbContext) and StackOverflow while the context is
    // being built.
    public DispatchDomainEventsInterceptor(IMediator mediator, IServiceProvider serviceProvider)
    {
        _mediator = mediator;
        _serviceProvider = serviceProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        DispatchOutboxAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await DispatchOutboxAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchPostCommitAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchPostCommitAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    // Pre-commit: enqueue integration events into the outbox on the same context; do NOT clear — the
    // post-commit pass still needs the events for the notification handlers. The dispatcher is resolved
    // here (not in the constructor) to avoid the DbContext<->IPublishEndpoint construction cycle; by the
    // time SaveChanges runs the context already exists, so this resolves the ambient scoped instances.
    private async Task DispatchOutboxAsync(DbContext? context, CancellationToken cancellationToken = default)
    {
        var domainEvents = CollectDomainEvents(context);
        if (domainEvents.Count == 0)
        {
            return;
        }

        var outboxDispatcher = _serviceProvider.GetRequiredService<IOutboxDomainEventDispatcher>();
        foreach (var domainEvent in domainEvents)
        {
            await outboxDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
    }

    // Post-commit: snapshot the events, CLEAR them from the entities, THEN run the SignalR/orchestration
    // notification handlers off the snapshot. Clearing before the fan-out is essential: a handler can
    // trigger a re-entrant SaveChanges on this same scoped context (e.g. activating the next question);
    // if the original events were still tracked, that nested save would re-collect and re-dispatch them —
    // duplicate SignalR broadcasts and a duplicate outbox insert.
    private async Task DispatchPostCommitAsync(DbContext? context, CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            return;
        }

        var entities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .Select(entry => entry.Entity)
            .ToList();

        var domainEvents = entities.SelectMany(entity => entity.DomainEvents).ToList();
        entities.ForEach(entity => entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }

    private static List<BaseEvent> CollectDomainEvents(DbContext? context)
    {
        if (context is null)
        {
            return [];
        }

        return context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToList();
    }
}
