# SignalR WebSockets .NET Examples

These examples are templates, not a framework. Adapt names, namespaces, DTOs, auth policies, and boundaries to the target project.

## Basic Registration

```csharp
using Microsoft.AspNetCore.Http.Connections;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<NotificationsHub>("/hubs/notifications", options =>
{
    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
});

app.Run();
```

## Strongly Typed Client Contract

```csharp
public interface INotificationsClient
{
    Task OrderUpdated(OrderUpdatedDto message);
    Task PresenceChanged(PresenceChangedDto message);
}

public sealed record OrderUpdatedDto(Guid OrderId, string Status, DateTimeOffset UpdatedAtUtc);

public sealed record PresenceChangedDto(string UserId, bool IsOnline);
```

## Thin Hub with Auth and Group Join

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public sealed class NotificationsHub : Hub<INotificationsClient>
{
    private readonly IRoomAuthorizationService _roomAuthz;
    private readonly IPresenceTracker _presence;

    public NotificationsHub(IRoomAuthorizationService roomAuthz, IPresenceTracker presence)
    {
        _roomAuthz = roomAuthz;
        _presence = presence;
    }

    public override async Task OnConnectedAsync()
    {
        await _presence.MarkConnectedAsync(
            Context.UserIdentifier!,
            Context.ConnectionId,
            Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _presence.MarkDisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId)
    {
        var user = Context.User ?? throw new HubException("Unauthenticated");

        if (!await _roomAuthz.CanJoinAsync(user, roomId, Context.ConnectionAborted))
        {
            throw new HubException("Not allowed to join this room.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Room(roomId), Context.ConnectionAborted);
    }
}
```

## Group Name Builder

```csharp
public static class GroupNames
{
    public static string Room(string roomId) => $"room:{roomId}";
    public static string TenantDashboard(string tenantId) => $"tenant-dashboard:{tenantId}";
}
```

## Publishing from Application Code with `IHubContext`

```csharp
using Microsoft.AspNetCore.SignalR;

public sealed class OrderStatusNotifier
{
    private readonly IHubContext<NotificationsHub, INotificationsClient> _hubContext;

    public OrderStatusNotifier(IHubContext<NotificationsHub, INotificationsClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyOrderUpdatedAsync(
        string userId,
        Guid orderId,
        string status,
        CancellationToken cancellationToken)
    {
        var dto = new OrderUpdatedDto(orderId, status, DateTimeOffset.UtcNow);

        return _hubContext.Clients.User(userId).OrderUpdated(dto);
    }
}
```

## Hub Filter for Logging and Timing

```csharp
using Microsoft.AspNetCore.SignalR;

public sealed class HubLoggingFilter : IHubFilter
{
    private readonly ILogger<HubLoggingFilter> _logger;

    public HubLoggingFilter(ILogger<HubLoggingFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            return await next(invocationContext);
        }
        finally
        {
            _logger.LogInformation(
                "Hub method {Hub}.{Method} for user {UserId} completed in {ElapsedMs} ms",
                invocationContext.Hub.GetType().Name,
                invocationContext.HubMethodName,
                invocationContext.Context.UserIdentifier,
                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
        }
    }
}
```

## Registering the Filter

```csharp
builder.Services.AddSignalR(options =>
{
    options.AddFilter<HubLoggingFilter>();
});
```

## Background Worker Publishing Progress

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

public sealed class JobProgressBroadcaster : BackgroundService
{
    private readonly IHubContext<NotificationsHub, INotificationsClient> _hubContext;
    private readonly IJobProgressFeed _feed;

    public JobProgressBroadcaster(
        IHubContext<NotificationsHub, INotificationsClient> hubContext,
        IJobProgressFeed feed)
    {
        _hubContext = hubContext;
        _feed = feed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _feed.ReadAllAsync(stoppingToken))
        {
            await _hubContext.Clients.User(item.UserId).OrderUpdated(
                new OrderUpdatedDto(item.OrderId, item.Status, item.UpdatedAtUtc));
        }
    }
}
```

## JavaScript Client with Automatic Reconnect

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => authStore.getAccessToken()
  })
  .withAutomaticReconnect([0, 2000, 10000, 30000])
  .configureLogging(signalR.LogLevel.Information)
  .build();

connection.on("OrderUpdated", (message) => {
  renderOrder(message);
});

connection.onreconnecting(() => {
  showConnectionBanner("Reconnecting...");
});

connection.onreconnected(async () => {
  await rejoinAuthorizedRooms();
  await reloadCurrentState();
});

connection.onclose(() => {
  showConnectionBanner("Disconnected");
});

await connection.start();
```

## .NET Client with Automatic Reconnect

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("https://example.com/hubs/notifications", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(tokenProvider.GetToken())!;
    })
    .WithAutomaticReconnect()
    .Build();

connection.On<OrderUpdatedDto>("OrderUpdated", message =>
{
    Console.WriteLine($"Order {message.OrderId} -> {message.Status}");
});

connection.Reconnected += async _ =>
{
    await ReloadCurrentStateAsync();
};

await connection.StartAsync();
```

## Presence Tracker Boundary

```csharp
public interface IPresenceTracker
{
    Task MarkConnectedAsync(string userId, string connectionId, CancellationToken cancellationToken);
    Task MarkDisconnectedAsync(string connectionId);
}
```

## Recommended Recovery Flow

```text
1. Connect or reconnect.
2. Authenticate and resolve user identity.
3. Rejoin authorized groups from server-trusted state.
4. Fetch current state through an HTTP endpoint or bootstrap hub method.
5. Resume incremental live updates.
```
