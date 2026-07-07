using System.Reflection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Identity;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;
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
        });

        builder.Services.AddScoped<ICurrentActor, CurrentActor>();

        builder.Services.AddScoped<IdentityProvisioningPolicy>();
        builder.Services.AddScoped<AccessPolicy>();

        // Concrete use-case handlers (the real subjects). Each is wrapped by its mandated
        // *AuthorizationProxy, which MediatR's assembly scan discovers as the IRequestHandler
        // for the request — the guard therefore runs before the handler. The concrete handler
        // is deliberately not an IRequestHandler, so the scan registers only the Proxy.
        builder.Services.AddScoped<AssignUserRoleCommandHandler>();
        builder.Services.AddScoped<ValidateParticipantMembershipAccessQueryHandler>();
    }
}
