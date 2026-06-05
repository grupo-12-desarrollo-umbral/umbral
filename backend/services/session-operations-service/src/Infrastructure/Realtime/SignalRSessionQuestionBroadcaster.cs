using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Infrastructure.Realtime;

public sealed class SignalRSessionQuestionBroadcaster : ISessionQuestionBroadcaster
{
    public const string QuestionActivatedMethod = "QuestionActivated";
    public const string QuestionClosedMethod = "QuestionClosed";

    private const string SessionsHubTypeName = "umbral_backend.Api.Hubs.SessionsHub";

    private readonly IServiceProvider _serviceProvider;

    public SignalRSessionQuestionBroadcaster(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task BroadcastQuestionActivatedAsync(
        QuestionActivatedNotificationDto notification,
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
            .SendCoreAsync(QuestionActivatedMethod, [notification], cancellationToken);
    }

    public Task BroadcastQuestionClosedAsync(
        QuestionClosedNotificationDto notification,
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
            .SendCoreAsync(QuestionClosedMethod, [notification], cancellationToken);
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
