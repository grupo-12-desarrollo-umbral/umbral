using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Identity;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddPersistenceServices();
        builder.Services.AddSingleton(TimeProvider.System);

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
    }
}
