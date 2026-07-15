using System.Reflection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Common;
using umbral_backend.Domain.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        builder.Services.AddScoped<IScorePolicy, SnapshotScorePolicy>();
        builder.Services.AddScoped<IRankingPolicy, ResolutionTimeRankingPolicy>();
        builder.Services.AddScoped<IRankingSessionMembershipGuard, RankingSessionMembershipGuard>();
    }
}
