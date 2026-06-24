using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Realtime;

public sealed class SignalRSessionTimerBroadcaster : ISessionTimerBroadcaster
{
    public const string TimerUpdatedMethod = "SessionTimerUpdated";

    private const string SessionsHubTypeName = "umbral_backend.Api.Hubs.SessionsHub";

    private readonly IServiceProvider _serviceProvider;

    public SignalRSessionTimerBroadcaster(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task BroadcastTimerUpdatedAsync(
        SessionTimerUpdatedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        var hubContext = ResolveSessionsHubContext();
        if (hubContext is null)
        {
            return Task.CompletedTask;
        }

        var clients = (IHubClients)hubContext.GetType()
            .GetProperty(nameof(IHubContext<Hub>.Clients))!
            .GetValue(hubContext)!;

        return clients
            .Group($"live-session:{notification.LiveSessionId:D}")
            .SendCoreAsync(TimerUpdatedMethod, [notification], cancellationToken);
    }

    private object? ResolveSessionsHubContext()
    {
        var hubType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(SessionsHubTypeName, throwOnError: false))
            .FirstOrDefault(type => type is not null);

        if (hubType is null)
        {
            return null;
        }

        var hubContextType = typeof(IHubContext<>).MakeGenericType(hubType);
        return _serviceProvider.GetService(hubContextType);
    }
}
