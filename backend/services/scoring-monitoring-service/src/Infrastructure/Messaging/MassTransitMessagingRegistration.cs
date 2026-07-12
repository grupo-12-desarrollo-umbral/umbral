using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using umbral_backend.Infrastructure.Messaging.Consumers;

namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// Registers the MassTransit bus over the RabbitMQ transport (#191) and binds the
/// <see cref="AnswerRegisteredConsumer"/> to the <c>session-answer-registered</c> exchange that
/// SessionOperations publishes to. Vanilla topology — the only customisation is the exchange name
/// carried by <c>[EntityName]</c> on the contract. Broker host/credentials come from
/// <see cref="RabbitMqOptions"/> (config, not hardcoded); the Generic Host starts/stops the bus.
/// </summary>
[ExcludeFromCodeCoverage] // Composition-root wiring — exercised end-to-end by the messaging integration test.
public static class MassTransitMessagingRegistration
{
    public static void AddMassTransitMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(bus =>
        {
            bus.AddConsumer<AnswerRegisteredConsumer>();

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
