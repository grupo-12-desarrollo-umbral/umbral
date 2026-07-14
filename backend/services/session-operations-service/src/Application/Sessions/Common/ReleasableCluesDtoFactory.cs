using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

public static class ReleasableCluesDtoFactory
{
    public static ReleasableCluesDto Create(LiveSession liveSession)
    {
        var clues = liveSession.ProjectReleasableClues()
            .Select(clue => new ReleasableClueDto(
                clue.TargetId,
                clue.ClueId,
                clue.TargetName,
                clue.SequenceOrder,
                clue.ClueText))
            .ToList();

        return new ReleasableCluesDto(
            liveSession.LiveSessionId,
            liveSession.ActiveSubstageId,
            clues);
    }
}
