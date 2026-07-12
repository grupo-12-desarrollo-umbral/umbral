using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace umbral_backend.Infrastructure.Messaging;

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
