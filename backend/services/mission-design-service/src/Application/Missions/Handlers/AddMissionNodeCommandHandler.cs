using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class AddMissionNodeCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<AddMissionNodeCommand, MissionDto>
{
    public AddMissionNodeCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(AddMissionNodeCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);

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

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }

    private static ClueVisibilityPolicy ParseClueVisibility(string? clueVisibilityPolicy)
    {
        return clueVisibilityPolicy is null
            ? ClueVisibilityPolicy.HiddenUntilOperatorRelease
            : Enum.Parse<ClueVisibilityPolicy>(clueVisibilityPolicy);
    }
}
