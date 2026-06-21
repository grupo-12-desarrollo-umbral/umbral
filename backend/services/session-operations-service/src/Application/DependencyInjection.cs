using System.Reflection;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Behaviours;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.CreateSession;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.Commands.DisconnectParticipant;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.Commands.TransitionSessionState;
using umbral_backend.Application.Sessions.Facades;
using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Application.Sessions.StateTransitions.Validators;
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

        builder.Services.AddSingleton<JoinPolicy>();
        builder.Services.AddSingleton<SessionCreationPolicy>();
        builder.Services.AddSingleton<SessionStateTransitionPolicy>();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

        builder.Services.AddScoped<ISessionAdministrationAccessExecutor, SessionAdministrationAccessResolver>();
        builder.Services.AddScoped<ISessionAdministrationAccessResolver, SessionAdministrationAuthorizationProxy>();
        builder.Services.AddScoped<ISessionTeamAssociationFacade, SessionTeamAssociationFacade>();
        builder.Services.AddScoped<IAssignOperatorToSessionFacade, AssignOperatorToSessionFacade>();
        builder.Services.AddScoped<ICreateSessionFacade, CreateSessionFacade>();
        builder.Services.AddScoped<IDisconnectParticipantExecutor, DisconnectParticipantService>();
        builder.Services.AddScoped<IDisconnectParticipantService, DisconnectParticipantAuthorizationProxy>();
        builder.Services.AddScoped<IReconnectAuthenticatedParticipantExecutor, ReconnectAuthenticatedParticipantService>();
        builder.Services.AddScoped<IReconnectAuthenticatedParticipantService, ReconnectAuthenticatedParticipantAuthorizationProxy>();
        builder.Services.AddScoped<ITransitionSessionStateFacade, TransitionSessionStateFacade>();
        builder.Services.AddScoped<ITriviaRoundOrchestratorFacade, TriviaRoundOrchestratorFacade>();
        builder.Services.AddScoped<IQuestionActivationStrategy, SequentialQuestionActivationStrategy>();

        // Chain of Responsibility for session-state transitions. Registration order is the run
        // order; downstream HUs append a validator here without modifying SessionTransitionChain.
        builder.Services.AddScoped<SessionTransitionValidator, CurrentStateGate>();
        builder.Services.AddScoped<SessionTransitionValidator, OperatorAssignmentGate>();
        builder.Services.AddScoped<SessionTransitionValidator, ParticipantReadinessGate>();
        builder.Services.AddScoped<SessionTransitionChain>();
    }
}
