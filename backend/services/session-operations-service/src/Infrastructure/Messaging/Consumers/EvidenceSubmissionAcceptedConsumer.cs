using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Messaging.Consumers;

// AC#5 / ddd_solution_model.md:615 — first audit/history consumer in this service;
// override of matrix transport table :59 "neither"
public sealed class EvidenceSubmissionAcceptedConsumer : IConsumer<EvidenceSubmissionAcceptedIntegrationEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<EvidenceSubmissionAcceptedConsumer> _logger;

    public EvidenceSubmissionAcceptedConsumer(ISender sender, ILogger<EvidenceSubmissionAcceptedConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EvidenceSubmissionAcceptedIntegrationEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consumed EvidenceSubmissionAccepted for evidence {EvidenceSubmissionId} session {LiveSessionId} team {TeamId}.",
            message.EvidenceSubmissionId,
            message.LiveSessionId,
            message.TeamId);

        await _sender.Send(
            new RecordEvidenceTraceResolutionCommand(
                message.EvidenceSubmissionId,
                message.LiveSessionId,
                message.TeamId,
                message.ActiveSubstageId,
                message.SubmissionType,
                message.SubmittedAt,
                EvidenceValidationState.Accepted,
                RejectionReason: null,
                message.ResolvedAt),
            context.CancellationToken);
    }
}
