using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Sessions.Commands.SubmitTriviaAnswer;

// Participant submits a team answer to the active trivia question (HU-34, DES-46). ONE vertical
// slice: accepting the first in-time answer and rejecting late/duplicate/stale attempts are the same
// invariant seen from either side — one command, one handler, no split accept/reject path. The
// snapshotted question is identified by its composite key (substage snapshot id + question sequence
// order) plus the selected option's sequence order, because the frozen snapshot carries no per-item
// Guids. Token carries the optional runtime-participation credential re-checked by the guard.
[Authorize(Roles = "Participant")]
public sealed record SubmitTriviaAnswerCommand(
    Guid LiveSessionId,
    Guid TeamId,
    Guid TriviaSubstageSnapshotId,
    int QuestionSequenceOrder,
    int SelectedOptionSequenceOrder,
    string? Token = null) : IRequest<SubmitTriviaAnswerResultDto>;
