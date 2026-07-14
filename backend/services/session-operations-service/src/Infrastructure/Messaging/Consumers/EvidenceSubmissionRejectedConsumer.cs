using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Messaging.Consumers;

// AC#5 / ddd_solution_model.md:615 — first audit/history consumer in this service;
// override of matrix transport table :59 "neither"
public sealed class EvidenceSubmissionRejectedConsumer : IConsumer<EvidenceSubmissionRejectedIntegrationEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<EvidenceSubmissionRejectedConsumer> _logger;

    public EvidenceSubmissionRejectedConsumer(ISender sender, ILogger<EvidenceSubmissionRejectedConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EvidenceSubmissionRejectedIntegrationEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consumed EvidenceSubmissionRejected for evidence {EvidenceSubmissionId} session {LiveSessionId} team {TeamId}: {RejectionReason}.",
            message.EvidenceSubmissionId,
            message.LiveSessionId,
            message.TeamId,
            message.RejectionReason);

        await _sender.Send(
            new RecordEvidenceTraceResolutionCommand(
                message.EvidenceSubmissionId,
                message.LiveSessionId,
                message.TeamId,
                message.ActiveSubstageId,
                message.SubmissionType,
                message.SubmittedAt,
                EvidenceValidationState.Rejected,
                message.RejectionReason,
                message.ResolvedAt),
            context.CancellationToken);
    }
}
