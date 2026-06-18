using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Commands.UpdateMissionNode;

[Authorize(Roles = Roles.Administrator)]
public sealed record UpdateMissionNodeCommand(
    int MissionId,
    int NodeId,
    string Title,
    int SequenceOrder,
    string? ClueText = null,
    string? ClueVisibilityPolicy = null) : IRequest<MissionDto>;
