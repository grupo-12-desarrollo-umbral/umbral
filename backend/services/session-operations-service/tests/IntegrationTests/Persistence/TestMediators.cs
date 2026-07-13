using MediatR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// No-op outbox dispatch for context-factory saves that do not exercise the transactional outbox
// (repository integration tests), mirroring NoOpMediator for the post-commit notification path.
internal sealed class NoOpOutboxDomainEventDispatcher : IOutboxDomainEventDispatcher
{
    public Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class NoOpMediator : IMediator
{
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        return Task.CompletedTask;
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        throw new NotSupportedException();
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
