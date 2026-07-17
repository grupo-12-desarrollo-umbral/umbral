using System.Reflection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Common;
using umbral_backend.Application.Scores.Common.Authorization;
using umbral_backend.Application.Scores.EventHandlers;
using umbral_backend.Domain.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
            // Innermost: a concurrency retry must re-run only the handler, and must sit inside
            // UnhandledExceptionBehaviour so a retried loss is not logged as an error.
            cfg.AddOpenBehavior(typeof(ConcurrencyRetryBehaviour<,>));
        });

        builder.Services.AddScoped<IScorePolicy, SnapshotScorePolicy>();
        builder.Services.AddScoped<IRankingPolicy, ResolutionTimeRankingPolicy>();
        builder.Services.AddScoped<IRankingSessionMembershipGuard, RankingSessionMembershipGuard>();
        builder.Services.AddScoped<IPenaltyPolicy, DefaultPenaltyPolicy>();

        builder.Services.AddScoped<IScoringSessionAccessResolver, ScoringSessionAuthorizationProxy>();
        builder.Services.AddScoped<PublishScoreEntryRegisteredIntegrationEventHandler>();
        builder.Services.AddScoped<IOutboxDomainEventDispatcher, OutboxDomainEventDispatcher>();
    }
}
