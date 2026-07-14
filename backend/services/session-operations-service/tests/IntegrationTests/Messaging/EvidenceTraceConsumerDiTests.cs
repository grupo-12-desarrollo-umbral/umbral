using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using umbral_backend.Infrastructure.Messaging.Consumers;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

public sealed class EvidenceTraceConsumerDiTests
{
    [Fact]
    public void EvidenceSubmissionRegisteredConsumer_ResolvesFromContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Mock.Of<ISender>());
        services.AddTransient<EvidenceSubmissionRegisteredConsumer>();

        var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<EvidenceSubmissionRegisteredConsumer>();
        act.Should().NotThrow();
    }

    [Fact]
    public void EvidenceSubmissionAcceptedConsumer_ResolvesFromContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Mock.Of<ISender>());
        services.AddTransient<EvidenceSubmissionAcceptedConsumer>();

        var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<EvidenceSubmissionAcceptedConsumer>();
        act.Should().NotThrow();
    }

    [Fact]
    public void EvidenceSubmissionRejectedConsumer_ResolvesFromContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Mock.Of<ISender>());
        services.AddTransient<EvidenceSubmissionRejectedConsumer>();

        var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<EvidenceSubmissionRejectedConsumer>();
        act.Should().NotThrow();
    }
}
