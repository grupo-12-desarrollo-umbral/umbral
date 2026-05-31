using System.Diagnostics;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Common.Behaviours;

public sealed class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Stopwatch _timer = new();
    private readonly ILogger<TRequest> _logger;
    private readonly ICurrentUser _currentUser;

    public PerformanceBehaviour(ILogger<TRequest> logger, ICurrentUser currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        if (_timer.ElapsedMilliseconds > 500)
        {
            _logger.LogWarning(
                "Long running request {RequestName} for user {UserId} after {ElapsedMilliseconds} ms.",
                typeof(TRequest).Name,
                _currentUser.Id ?? string.Empty,
                _timer.ElapsedMilliseconds);
        }

        return response;
    }
}
