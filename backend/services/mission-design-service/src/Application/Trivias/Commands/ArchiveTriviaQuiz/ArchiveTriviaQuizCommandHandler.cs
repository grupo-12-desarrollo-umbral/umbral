using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;

public sealed class ArchiveTriviaQuizCommandHandler : IRequestHandler<ArchiveTriviaQuizCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly IClock _clock;

    public ArchiveTriviaQuizCommandHandler(
        ITriviaQuizRepository triviaQuizRepository,
        IMissionRepository missionRepository,
        IClock clock)
    {
        _triviaQuizRepository = triviaQuizRepository;
        _missionRepository = missionRepository;
        _clock = clock;
    }

    public async Task<TriviaQuizDto> Handle(ArchiveTriviaQuizCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        // Archive-time enforcement: reject archival while an active mission still selects this quiz.
        await ActiveMissionTriviaReferenceGuard.EnsureNotReferencedByActiveMissionAsync(
            _missionRepository,
            triviaQuiz.Id,
            cancellationToken);

        triviaQuiz.Archive(_clock.UtcNow);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
