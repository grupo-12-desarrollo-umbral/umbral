using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.AddMissionNode;

[Authorize(Roles = Roles.Administrator)]
public sealed record AddMissionNodeCommand(
    int MissionId,
    string NodeType,
    string Title,
    int SequenceOrder,
    int? StageId = null,
    int? SubstageId = null,
    string? PlayMode = null,
    string? ClueText = null,
    string? ClueVisibilityPolicy = null) : IRequest<MissionDto>;
