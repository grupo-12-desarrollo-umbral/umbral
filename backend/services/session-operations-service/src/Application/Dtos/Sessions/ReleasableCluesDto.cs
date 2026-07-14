namespace umbral_backend.Application.Dtos.Sessions;

// Operator release-clue picker payload for the active substage. A clue is identified by exactly one
// of TargetId (treasure hunt) or ClueId (target-less trivia).
public sealed record ReleasableCluesDto(
    Guid LiveSessionId,
    Guid? ActiveSubstageId,
    IReadOnlyList<ReleasableClueDto> Clues);

public sealed record ReleasableClueDto(
    Guid? TargetId,
    Guid? ClueId,
    string? TargetName,
    int SequenceOrder,
    string ClueText);
