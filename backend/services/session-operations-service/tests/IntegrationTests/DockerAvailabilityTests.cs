using System.Net.Http;
using System.Net.Sockets;
using Xunit.Sdk;

namespace umbral_backend.Infrastructure.IntegrationTests;

public sealed class DockerAvailabilityTests
{
    [Fact]
    public async Task StartOrSkipAsync_WhenDockerDaemonPingCannotConnect_SkipsWithoutStartingContainer()
    {
        var startCalled = false;
        var disposed = false;

        Func<Task> act = () => DockerAvailability.StartOrSkipAsync(
            () =>
            {
                startCalled = true;
                return Task.CompletedTask;
            },
            () =>
            {
                disposed = true;
                return ValueTask.CompletedTask;
            },
            () => throw new HttpRequestException(
                "Docker endpoint unavailable",
                new SocketException((int)SocketError.ConnectionRefused)));

        await act.Should().ThrowAsync<SkipException>();
        startCalled.Should().BeFalse();
        disposed.Should().BeTrue();
    }

    [Fact]
    public async Task StartOrSkipAsync_WhenImagePullHasNestedSocketFailure_FailsInsteadOfSkipping()
    {
        var registryFailure = new HttpRequestException(
            "Image pull failed",
            new SocketException((int)SocketError.HostNotFound));

        Func<Task> act = () => DockerAvailability.StartOrSkipAsync(
            () => Task.FromException(registryFailure),
            () => ValueTask.CompletedTask,
            () => Task.CompletedTask);

        var assertion = await act.Should().ThrowAsync<HttpRequestException>();
        assertion.Which.Should().BeSameAs(registryFailure);
    }

    [Fact]
    public async Task StartOrSkipAsync_WhenBrokerStartupTimesOut_FailsInsteadOfSkipping()
    {
        var brokerStartupFailure = new TimeoutException("RabbitMQ did not become ready");

        Func<Task> act = () => DockerAvailability.StartOrSkipAsync(
            () => Task.FromException(brokerStartupFailure),
            () => ValueTask.CompletedTask,
            () => Task.CompletedTask);

        var assertion = await act.Should().ThrowAsync<TimeoutException>();
        assertion.Which.Should().BeSameAs(brokerStartupFailure);
    }
}
