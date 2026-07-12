using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// Registers the MassTransit bus over the RabbitMQ transport (#164). Vanilla topology and
/// formatters — the only customisation is a domain-meaningful exchange name via
/// <c>[EntityName]</c> on each contract. Broker host/credentials come from <see cref="RabbitMqOptions"/>
/// (config, not hardcoded); the Generic Host starts/stops the bus with the app.
/// </summary>
[ExcludeFromCodeCoverage] // Composition-root wiring — exercised end-to-end by the messaging integration test.
public static class MassTransitMessagingRegistration
{
    public static void AddMassTransitMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(bus =>
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                cfg.Host(options.HostName, (ushort)options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.UserName);
                    host.Password(options.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });
    }
}
