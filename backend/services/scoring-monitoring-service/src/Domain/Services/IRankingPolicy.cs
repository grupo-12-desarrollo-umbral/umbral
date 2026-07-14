using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

public interface IRankingPolicy
{
    IReadOnlyList<(Guid TeamId, int Position, int TotalScore, ResolutionTime ResolutionTime)> Rank(
        IEnumerable<(Guid TeamId, int TotalScore, ResolutionTime ResolutionTime)> teams);
}
