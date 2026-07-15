using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Identity;
using umbral_backend.Infrastructure.Messaging;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddPersistenceServices();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.Configure<RabbitMqOptions>(
            builder.Configuration.GetSection(RabbitMqOptions.SectionName));

        builder.AddMassTransitMessaging();

        builder.Services.Configure<SessionOperationsClientOptions>(
            builder.Configuration.GetSection(SessionOperationsClientOptions.SectionName));

        var sessionOpsBaseAddress = builder.Configuration
            .GetSection(SessionOperationsClientOptions.SectionName)
            .GetValue<string>(nameof(SessionOperationsClientOptions.BaseAddress))
            ?? new SessionOperationsClientOptions().BaseAddress;

        builder.Services.AddHttpClient<IParticipantSessionMembershipClient, ParticipantSessionMembershipClient>(client =>
        {
            client.BaseAddress = new Uri(sessionOpsBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}
