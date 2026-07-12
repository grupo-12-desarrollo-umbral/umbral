namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// Broker settings for the MassTransit bus (#191). Defaults target the compose <c>rabbitmq</c>
/// service; bound via <c>Configure&lt;RabbitMqOptions&gt;</c> from the <c>RabbitMq</c> section plus
/// the <c>RabbitMq__*</c> environment variables compose passes.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "rabbitmq";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string Exchange { get; set; } = "umbral.scoring-monitoring";
}
