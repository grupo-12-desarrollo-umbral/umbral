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
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation;
using umbral_backend.Application.Sessions.Common.EvidenceIntakeValidation.Validators;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation;
using umbral_backend.Application.Sessions.Common.TriviaAnswerValidation.Validators;
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
        builder.Services.AddSingleton<OpenTeamSelectionPolicy>();
        builder.Services.AddSingleton<SessionCreationPolicy>();
        builder.Services.AddSingleton<SessionStateTransitionPolicy>();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

        builder.Services.AddScoped<IRuntimeParticipationGuard, RuntimeParticipationGuard>();
        builder.Services.AddScoped<ISessionAdministrationAccessResolver, SessionAdministrationAuthorizationProxy>();
        builder.Services.AddScoped<ISessionTeamAssociationFacade, SessionTeamAssociationFacade>();
        builder.Services.AddScoped<ITriviaRoundOrchestratorFacade, TriviaRoundOrchestratorFacade>();
        builder.Services.AddScoped<IEvidenceIntakeFacade, EvidenceIntakeFacade>();
        builder.Services.AddScoped<IQuestionActivationStrategy, SequentialQuestionActivationStrategy>();

        // Transactional-outbox integration publishers dispatched pre-commit by the interceptor (not via
        // MediatR notifications, so their OutboxMessage insert rides the business SaveChanges).
        builder.Services.AddScoped<PublishAnswerRegisteredIntegrationEventHandler>();
        builder.Services.AddScoped<PublishEvidenceSubmissionRegisteredIntegrationEventHandler>();
        builder.Services.AddScoped<PublishQuestionClosedIntegrationEventHandler>();
        builder.Services.AddScoped<PublishSessionResultsFinalizedIntegrationEventHandler>();
        builder.Services.AddScoped<IOutboxDomainEventDispatcher, OutboxDomainEventDispatcher>();

        // Chain of Responsibility for session-state transitions. Registration order is the run
        // order; downstream HUs append a validator here without modifying SessionTransitionChain.
        builder.Services.AddScoped<SessionTransitionValidator, CurrentStateGate>();
        builder.Services.AddScoped<SessionTransitionValidator, OperatorAssignmentGate>();
        builder.Services.AddScoped<SessionTransitionValidator, ParticipantReadinessGate>();
        builder.Services.AddScoped<SessionTransitionChain>();

        // Shared evidence-intake Chain of Responsibility. Registration order IS the run order and
        // each rejecting link short-circuits the rest.
        builder.Services.AddScoped<EvidenceIntakeValidationLink, RuntimeParticipationLink>();
        builder.Services.AddScoped<EvidenceIntakeValidationLink, SessionAdmitsReceptionLink>();
        builder.Services.AddScoped<EvidenceIntakeValidationLink, ActiveSubstagePresentLink>();
        builder.Services.AddScoped<EvidenceIntakeValidationChain>();

        // Trivia composes the shared admission chain above with these form-specific extension links.
        builder.Services.AddScoped<TriviaAnswerValidationLink, ActiveTriviaQuestionLink>();
        builder.Services.AddScoped<TriviaAnswerValidationLink, TriviaAnswerWindowLink>();
        builder.Services.AddScoped<TriviaAnswerValidationLink, DuplicateTriviaAnswerLink>();
        builder.Services.AddScoped<TriviaAnswerValidationChain>();
    }
}
