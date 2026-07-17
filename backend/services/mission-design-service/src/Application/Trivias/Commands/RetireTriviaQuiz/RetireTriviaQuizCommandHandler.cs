using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Common.Authoring;

namespace umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;

public sealed class RetireTriviaQuizCommandHandler : IRequestHandler<RetireTriviaQuizCommand, TriviaQuizDto>
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly IClock _clock;

    public RetireTriviaQuizCommandHandler(
        ITriviaQuizRepository triviaQuizRepository,
        IMissionRepository missionRepository,
        IClock clock)
    {
        _triviaQuizRepository = triviaQuizRepository;
        _missionRepository = missionRepository;
        _clock = clock;
    }

    public async Task<TriviaQuizDto> Handle(RetireTriviaQuizCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        // ADR-0003: retirement lands on Archived like archival does, so it carries the same
        // active-mission reference check.
        await ActiveMissionTriviaReferenceGuard.EnsureNotReferencedByActiveMissionAsync(
            _missionRepository,
            triviaQuiz.Id,
            cancellationToken);

        triviaQuiz.RetireFromFutureUse(_clock.UtcNow);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }
}
