using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Commands.AddMissionNode;

public sealed class AddMissionNodeCommandHandler
    : IRequestHandler<AddMissionNodeCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public AddMissionNodeCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(AddMissionNodeCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);

        switch (request.NodeType)
        {
            case "Stage":
                mission.AddStage(request.Title, request.SequenceOrder);
                break;
            case "Substage":
                MissionStructureEditor.FindStage(mission, request.StageId!.Value);
                mission.AddSubstage(
                    request.StageId.Value,
                    request.PlayMode == SubstagePlayMode.Trivia.ToString()
                        ? Substage.CreateTrivia(request.Title, request.SequenceOrder)
                        : Substage.CreateTreasureHunt(request.Title, request.SequenceOrder));
                break;
            case "Clue":
                MissionStructureEditor.FindSubstage(mission, request.StageId!.Value, request.SubstageId!.Value);
                mission.AddClue(
                    request.StageId.Value,
                    request.SubstageId.Value,
                    Clue.Create(
                        request.Title,
                        request.SequenceOrder,
                        request.ClueText ?? string.Empty,
                        ParseClueVisibility(request.ClueVisibilityPolicy)));
                break;
        }

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }

    private static ClueVisibilityPolicy ParseClueVisibility(string? clueVisibilityPolicy)
    {
        return clueVisibilityPolicy is null
            ? ClueVisibilityPolicy.HiddenUntilOperatorRelease
            : Enum.Parse<ClueVisibilityPolicy>(clueVisibilityPolicy);
    }
}
