using System.Net.Http;
using System.Net.Sockets;
using DotNet.Testcontainers.Configurations;
using Xunit.Sdk;

namespace umbral_backend.Infrastructure.IntegrationTests;

internal static class DockerAvailability
{
    public static async Task StartOrSkipAsync(Func<Task> start, Func<ValueTask> dispose)
    {
        try
        {
            using var configuration =
                TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientConfiguration(Guid.NewGuid());
            using var client = configuration.CreateClient();
            await client.System.PingAsync();
        }
        catch (Exception exception) when (ContainsSocketFailure(exception))
        {
            await dispose();
            throw SkipException.ForSkip(
                "Docker is unavailable: Testcontainers could not connect to the Docker daemon.");
        }

        await start();
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
