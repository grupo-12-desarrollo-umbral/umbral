using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// Registers the MassTransit bus over the RabbitMQ transport (#164), fronted by the EF Core
/// transactional bus outbox. Broker host/credentials come from <see cref="MassTransitRabbitMqOptions"/>
/// (config, not hardcoded); the Generic Host starts/stops the bus with the app. With the bus outbox
/// wired, the scoped <c>IPublishEndpoint</c> inserts an <c>OutboxMessage</c> row into the tracked
/// <see cref="ApplicationDbContext"/> instead of touching RabbitMQ on the hot path, so the publish
/// commits atomically with the business write and a broker outage never stalls or loses the event —
/// the hosted delivery service drains the outbox to RabbitMQ asynchronously.
/// </summary>
[ExcludeFromCodeCoverage] // Composition-root wiring — exercised end-to-end by the messaging integration test.
public static class MassTransitMessagingRegistration
{
    public static void AddMassTransitMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<ApplicationDbContext>(outbox =>
            {
                outbox.UsePostgres();                                    // Npgsql row-lock semantics for the delivery service
                outbox.QueryDelay = TimeSpan.FromSeconds(1);             // delivery-service poll interval
                outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30); // MessageId dedup window on delivery
                outbox.UseBusOutbox();                                   // scoped IPublishEndpoint -> DB insert
            });

            bus.UsingRabbitMq((context, cfg) =>
            {
                var options = context.GetRequiredService<IOptions<MassTransitRabbitMqOptions>>().Value;

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
