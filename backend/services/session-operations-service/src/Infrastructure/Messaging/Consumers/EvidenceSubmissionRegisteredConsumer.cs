using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Messaging.Consumers;

// AC#5 / ddd_solution_model.md:615 — first audit/history consumer in this service;
// override of matrix transport table :59 "neither"
public sealed class EvidenceSubmissionRegisteredConsumer : IConsumer<EvidenceSubmissionRegisteredIntegrationEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<EvidenceSubmissionRegisteredConsumer> _logger;

    public EvidenceSubmissionRegisteredConsumer(ISender sender, ILogger<EvidenceSubmissionRegisteredConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EvidenceSubmissionRegisteredIntegrationEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consumed EvidenceSubmissionRegistered for evidence {EvidenceSubmissionId} session {LiveSessionId} team {TeamId}.",
            message.EvidenceSubmissionId,
            message.LiveSessionId,
            message.TeamId);

        await _sender.Send(
            new RecordEvidenceTraceRegistrationCommand(
                message.EvidenceSubmissionId,
                message.LiveSessionId,
                message.TeamId,
                message.ActiveSubstageId,
                message.SubmissionType,
                SubmittedByParticipantId: null,
                message.OriginReference,
                message.SubmittedAt),
            context.CancellationToken);
    }
}
