namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// Broker settings for the outbound integration-event publisher (HU-33B, X.3). Defaults
/// target the compose <c>rabbitmq</c> service; bound via <c>Configure&lt;RabbitMqOptions&gt;</c>.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "rabbitmq";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string Exchange { get; set; } = "umbral.session-operations";

    /// <summary>
    /// Caps how long a single publisher-confirm round-trip may await the broker ack before the
    /// drain loop gives up on that message (best-effort, D-3). Bounds the hung-broker case: a
    /// broker reachable over TCP but never acking must not block the drain task — or shutdown —
    /// forever. Binds from a <c>TimeSpan</c> string (e.g. <c>"00:00:05"</c>).
    /// </summary>
    public TimeSpan PublishConfirmTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
