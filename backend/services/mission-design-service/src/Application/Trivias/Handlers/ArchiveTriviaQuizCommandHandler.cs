using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Lifecycle;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class ArchiveTriviaQuizCommandHandler : TriviaQuizLifecycleCommandHandler<ArchiveTriviaQuizCommand>
{
    private readonly IMissionRepository _missionRepository;

    public ArchiveTriviaQuizCommandHandler(
        ITriviaQuizRepository triviaQuizRepository,
        IMissionRepository missionRepository,
        IClock clock)
        : base(triviaQuizRepository, clock)
    {
        _missionRepository = missionRepository;
    }

    // Archive-time enforcement: reject archival while an active mission still selects this quiz.
    protected override Task EnsureTransitionAllowedAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
        => ActiveMissionTriviaReferenceGuard.EnsureNotReferencedByActiveMissionAsync(
            _missionRepository,
            triviaQuiz.Id,
            cancellationToken);

    protected override void ApplyTransition(TriviaQuiz triviaQuiz, DateTimeOffset transitionedAt)
    {
        triviaQuiz.Archive(transitionedAt);
    }
}
