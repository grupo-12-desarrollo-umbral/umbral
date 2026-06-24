using System.Reflection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.JoinTokens.Commands.IssueJoinToken;
using umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;
using umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;
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

        builder.Services.AddSingleton<JoinTokenPolicy>();
        builder.Services.AddScoped<IdentityProvisioningPolicy>();
        builder.Services.AddScoped<AccessPolicy>();

        builder.Services.AddScoped<ParticipantSessionMembershipPolicy>();
        builder.Services.AddScoped<UserRoleAssignmentService>();
        builder.Services.AddScoped<IUserRoleAssignmentService>(sp =>
            ActivatorUtilities.CreateInstance<UserRoleAssignmentAuthorizationProxy>(
                sp, sp.GetRequiredService<UserRoleAssignmentService>()));
        builder.Services.AddScoped<JoinTokenIssuanceService>();
        builder.Services.AddScoped<IIssueJoinTokenService>(sp =>
            ActivatorUtilities.CreateInstance<JoinTokenIssuanceAuthorizationProxy>(
                sp, sp.GetRequiredService<JoinTokenIssuanceService>()));
        builder.Services.AddScoped<ParticipantMembershipAccessValidationService>();
        builder.Services.AddScoped<IValidateParticipantMembershipAccessService>(sp =>
            ActivatorUtilities.CreateInstance<ParticipantMembershipAccessAuthorizationProxy>(
                sp, sp.GetRequiredService<ParticipantMembershipAccessValidationService>()));
        builder.Services.AddScoped<ParticipantSessionTeamLobbyService>();
        builder.Services.AddScoped<IGetSessionTeamsForParticipantService>(sp =>
            ActivatorUtilities.CreateInstance<ParticipantSessionTeamLobbyAuthorizationProxy>(
                sp, sp.GetRequiredService<ParticipantSessionTeamLobbyService>()));
        builder.Services.AddScoped<ParticipantTeamSelfJoinService>();
        builder.Services.AddScoped<IJoinTeamAsParticipantService>(sp =>
            ActivatorUtilities.CreateInstance<ParticipantTeamSelfJoinAuthorizationProxy>(
                sp, sp.GetRequiredService<ParticipantTeamSelfJoinService>()));
    }
}
