using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Sessions.Commands.AddOperativeClue;

public sealed class AddOperativeClueCommandHandler
    : IRequestHandler<AddOperativeClueCommand, AddOperativeClueResultDto>
{
    private readonly ISessionAdministrationAccessResolver _sessionAdministrationAccessResolver;
    private readonly IAuthenticatedActorProfileAccessClient _authenticatedActorProfileAccessClient;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TimeProvider _timeProvider;

    public AddOperativeClueCommandHandler(
        ISessionAdministrationAccessResolver sessionAdministrationAccessResolver,
        IAuthenticatedActorProfileAccessClient authenticatedActorProfileAccessClient,
        ILiveSessionRepository liveSessionRepository,
        TimeProvider timeProvider)
    {
        _sessionAdministrationAccessResolver = sessionAdministrationAccessResolver;
        _authenticatedActorProfileAccessClient = authenticatedActorProfileAccessClient;
        _liveSessionRepository = liveSessionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AddOperativeClueResultDto> Handle(
        AddOperativeClueCommand request,
        CancellationToken cancellationToken)
    {
        var liveSession = await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId,
            cancellationToken);
        var actor = await _authenticatedActorProfileAccessClient.GetCurrentAsync(cancellationToken);
        var existingClueCount = liveSession.GetOperativeClues().Count;

        liveSession.AddOperativeClue(
            request.ClueText,
            request.TeamIds,
            actor.UserId,
            _timeProvider.GetUtcNow());

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        var addedClues = liveSession.GetOperativeClues().Skip(existingClueCount).ToArray();

        return new AddOperativeClueResultDto(
            addedClues.Select(clue => clue.OperativeClueId).ToArray(),
            addedClues.Select(clue => clue.TeamId).ToArray(),
            addedClues[0].ClueText);
    }
}
