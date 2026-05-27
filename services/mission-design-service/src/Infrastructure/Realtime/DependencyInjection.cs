using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Realtime;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class RealtimeServiceExtensions
{
    public static void AddRealtimeServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSignalR();
        builder.Services.AddScoped<INotifier, SignalRNotifier>();
    }
}
