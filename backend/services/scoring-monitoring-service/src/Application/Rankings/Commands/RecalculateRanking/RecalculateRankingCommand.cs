namespace umbral_backend.Application.Rankings.Commands.RecalculateRanking;

public sealed record RecalculateRankingCommand(
    Guid LiveSessionId,
    DateTimeOffset GeneratedAt) : IRequest;
