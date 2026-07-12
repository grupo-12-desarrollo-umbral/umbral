namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ host settings used by MassTransit's transport registration.
/// </summary>
public sealed class MassTransitRabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "rabbitmq";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";
}
