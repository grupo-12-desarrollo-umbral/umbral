using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Integrations.MissionDesign;
using umbral_backend.Infrastructure.Identity;
using umbral_backend.Infrastructure.Realtime;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddPersistenceServices();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISessionTimerBroadcaster, SignalRSessionTimerBroadcaster>();
        builder.Services.AddHostedService<AuthoritativeSessionTimerWorker>();

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

        builder.Services.Configure<PublishedTriviaQuizSourceOptions>(
            builder.Configuration.GetSection(PublishedTriviaQuizSourceOptions.SectionName));

        var missionDesignBaseAddress = builder.Configuration
            .GetSection(PublishedTriviaQuizSourceOptions.SectionName)
            .GetValue<string>(nameof(PublishedTriviaQuizSourceOptions.BaseAddress))
            ?? new PublishedTriviaQuizSourceOptions().BaseAddress;

        builder.Services.AddHttpClient<IPublishedTriviaQuizSource, PublishedTriviaQuizSource>(client =>
        {
            client.BaseAddress = new Uri(missionDesignBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}
