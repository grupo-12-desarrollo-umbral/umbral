using System.Net.Http;
using System.Net.Sockets;
using DotNet.Testcontainers.Configurations;
using Xunit.Sdk;

namespace umbral_backend.ScoringMonitoring.IntegrationTests;

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
