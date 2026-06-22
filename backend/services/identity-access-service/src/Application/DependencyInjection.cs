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
        builder.Services.AddScoped<IUserRoleAssignmentExecutor, UserRoleAssignmentService>();
        builder.Services.AddScoped<IUserRoleAssignmentService, UserRoleAssignmentAuthorizationProxy>();
        builder.Services.AddScoped<IIssueJoinTokenExecutor, JoinTokenIssuanceService>();
        builder.Services.AddScoped<IIssueJoinTokenService, JoinTokenIssuanceAuthorizationProxy>();
        builder.Services.AddScoped<IValidateParticipantMembershipAccessExecutor, ParticipantMembershipAccessValidationService>();
        builder.Services.AddScoped<IValidateParticipantMembershipAccessService, ParticipantMembershipAccessAuthorizationProxy>();
        builder.Services.AddScoped<IGetSessionTeamsForParticipantExecutor, ParticipantSessionTeamLobbyService>();
        builder.Services.AddScoped<IGetSessionTeamsForParticipantService, ParticipantSessionTeamLobbyAuthorizationProxy>();
        builder.Services.AddScoped<IJoinTeamAsParticipantExecutor, ParticipantTeamSelfJoinService>();
        builder.Services.AddScoped<IJoinTeamAsParticipantService, ParticipantTeamSelfJoinAuthorizationProxy>();
    }
}
