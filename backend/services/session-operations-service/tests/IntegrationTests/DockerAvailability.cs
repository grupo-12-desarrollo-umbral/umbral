using System.Net.Http;
using System.Net.Sockets;
using DotNet.Testcontainers.Configurations;
using Xunit.Sdk;

namespace umbral_backend.Infrastructure.IntegrationTests;

internal static class DockerAvailability
{
    private const string SkipReason =
        "Docker is unavailable: Testcontainers could not connect to the Docker daemon.";

    public static async Task StartOrSkipAsync(Func<Task> start, Func<ValueTask> dispose)
    {
        await StartOrSkipAsync(start, dispose, PingDockerDaemonAsync);
    }

    internal static async Task StartOrSkipAsync(
        Func<Task> start,
        Func<ValueTask> dispose,
        Func<Task> pingDockerDaemon)
    {
        try
        {
            await pingDockerDaemon();
        }
        catch (Exception exception) when (ContainsSocketFailure(exception))
        {
            await dispose();
            throw SkipException.ForSkip(SkipReason);
        }

        // Once the daemon has answered its API ping, failures here belong to image pull,
        // authentication, container configuration, port binding, or broker startup.
        // They must escape so the integration test cannot turn green without exercising RabbitMQ.
        await start();
    }

    private static async Task PingDockerDaemonAsync()
    {
        using var dockerClientConfiguration =
            TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientConfiguration(Guid.NewGuid());
        using var dockerClient = dockerClientConfiguration.CreateClient();
        await dockerClient.System.PingAsync();
    }

    private static bool ContainsSocketFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { InnerException: SocketException })
            {
                return true;
            }
        }

        return false;
    }
}
