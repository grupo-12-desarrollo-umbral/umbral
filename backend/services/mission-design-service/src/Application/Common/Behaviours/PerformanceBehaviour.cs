using System.Diagnostics;
using umbral_backend.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace umbral_backend.Application.Common.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Stopwatch _timer;
    private readonly ILogger<TRequest> _logger;
    private readonly ICurrentUser _user;
    private readonly IIdentityService _identityService;

    public PerformanceBehaviour(
        ILogger<TRequest> logger,
        ICurrentUser user,
        IIdentityService identityService)
    {
        _timer = new Stopwatch();

        _logger = logger;
        _user = user;
        _identityService = identityService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > 500)
        {
            var requestName = typeof(TRequest).Name;
            var userId = _user.Id ?? string.Empty;
            var userName = string.Empty;

            if (!string.IsNullOrEmpty(userId))
            {
                userName = await _identityService.GetUserNameAsync(userId);
            }

            // Name and actor only — no {@Request} body. Slow-request diagnostics never need the
            // gameplay payload (QR codes, coordinates, trivia answers), and logging it would leak it. (#7)
            _logger.LogWarning("umbral_backend Long Running Request: {Name} ({ElapsedMilliseconds} milliseconds) {UserId} {UserName}",
                requestName, elapsedMilliseconds, userId, userName);
        }

        return response;
    }
}
