using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Integrations.MissionDesign;
using umbral_backend.Infrastructure.Identity;
using umbral_backend.Infrastructure.Messaging;
using umbral_backend.Infrastructure.Realtime;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddPersistenceServices();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<AuthoritativeSessionTimerWorker>();

        builder.Services.Configure<RabbitMqOptions>(
            builder.Configuration.GetSection(RabbitMqOptions.SectionName));

        // QuestionClosed publishes via MassTransit (#164); the hand-rolled publisher still carries
        // AnswerRegistered + SessionResultsFinalized until #165/#166 migrate them.
        builder.AddMassTransitMessaging();
        builder.Services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();

        builder.Services.Configure<ParticipantMembershipAccessClientOptions>(
            builder.Configuration.GetSection(ParticipantMembershipAccessClientOptions.SectionName));

        var identityAccessBaseAddress = builder.Configuration
            .GetSection(ParticipantMembershipAccessClientOptions.SectionName)
            .GetValue<string>(nameof(ParticipantMembershipAccessClientOptions.BaseAddress))
            ?? new ParticipantMembershipAccessClientOptions().BaseAddress;

        builder.Services.AddHttpClient<IParticipantMembershipAccessClient, ParticipantMembershipAccessClient>(client =>
        {
            client.BaseAddress = new Uri(identityAccessBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.AddHttpClient<IAssignableSessionOperatorAccessClient, AssignableSessionOperatorAccessClient>(client =>
        {
            client.BaseAddress = new Uri(identityAccessBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.AddHttpClient<ITeamReferenceCatalogClient, TeamReferenceCatalogClient>(client =>
        {
            client.BaseAddress = new Uri(identityAccessBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.AddHttpClient<IAuthenticatedActorProfileAccessClient, AuthenticatedActorProfileAccessClient>(client =>
        {
            client.BaseAddress = new Uri(identityAccessBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.AddHttpClient<IParticipantEligibleTeamsClient, ParticipantEligibleTeamsClient>(client =>
        {
            client.BaseAddress = new Uri(identityAccessBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.Configure<MissionRuntimeSourceOptions>(
            builder.Configuration.GetSection(MissionRuntimeSourceOptions.SectionName));

        var missionDesignBaseAddress = builder.Configuration
            .GetSection(MissionRuntimeSourceOptions.SectionName)
            .GetValue<string>(nameof(MissionRuntimeSourceOptions.BaseAddress))
            ?? new MissionRuntimeSourceOptions().BaseAddress;

        builder.Services.AddHttpClient<IMissionRuntimeSource, MissionRuntimeSource>(client =>
        {
            client.BaseAddress = new Uri(missionDesignBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.Configure<MissionReadinessSourceOptions>(
            builder.Configuration.GetSection(MissionReadinessSourceOptions.SectionName));

        var missionReadinessBaseAddress = builder.Configuration
            .GetSection(MissionReadinessSourceOptions.SectionName)
            .GetValue<string>(nameof(MissionReadinessSourceOptions.BaseAddress))
            ?? missionDesignBaseAddress;

        builder.Services.AddHttpClient<IMissionReadinessSource, MissionReadinessSource>(client =>
        {
            client.BaseAddress = new Uri(missionReadinessBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}
