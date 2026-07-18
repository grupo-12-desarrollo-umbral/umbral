using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace umbral_backend.Application.UnitTests.Application.Common.Behaviours;

public sealed class PerformanceBehaviourTests
{
    [Fact]
    public async Task Handle_WhenRequestIsFast_DoesNotLogWarning()
    {
        var logger = new Mock<ILogger<SampleRequest>>();
        var behaviour = new PerformanceBehaviour<SampleRequest, string>(
            logger.Object,
            new StubCurrentUser("kc-fast"));

        var result = await behaviour.Handle(
            new SampleRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenRequestIsSlow_LogsWarning()
    {
        var logger = new Mock<ILogger<SampleRequest>>();
        var behaviour = new PerformanceBehaviour<SampleRequest, string>(
            logger.Object,
            new StubCurrentUser("kc-slow"));

        var result = await behaviour.Handle(
            new SampleRequest(),
            async () =>
            {
                await Task.Delay(550);
                return "slow";
            },
            CancellationToken.None);

        result.Should().Be("slow");
        logger.VerifyLog(LogLevel.Warning, "Long running request");
    }

    internal sealed record SampleRequest;

    private sealed record StubCurrentUser(string? Id) : ICurrentUser
    {
        public string? Email => null;

        public string? Role => null;
    }
}
