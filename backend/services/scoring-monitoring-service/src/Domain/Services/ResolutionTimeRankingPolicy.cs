using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services;

public sealed class ResolutionTimeRankingPolicy : IRankingPolicy
{
    public IReadOnlyList<(Guid TeamId, int Position, int TotalScore, ResolutionTime ResolutionTime)> Rank(
        IEnumerable<(Guid TeamId, int TotalScore, ResolutionTime ResolutionTime)> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);

        var orderedTeams = teams
            .Select(team => (team.TeamId, team.TotalScore, ResolutionTime: team.ResolutionTime ?? ResolutionTime.NonComparable()))
            .OrderByDescending(team => team.TotalScore)
            .ThenBy(team => team.ResolutionTime.IsComparable ? 0 : 1)
            .ThenBy(team => team.ResolutionTime.Value)
            .ToList();

        if (orderedTeams.Count == 0)
        {
            return Array.Empty<(Guid TeamId, int Position, int TotalScore, ResolutionTime ResolutionTime)>();
        }

        var ranked = new List<(Guid TeamId, int Position, int TotalScore, ResolutionTime ResolutionTime)>(orderedTeams.Count);

        for (var index = 0; index < orderedTeams.Count; index++)
        {
            var current = orderedTeams[index];
            var position = index + 1;

            if (index > 0)
            {
                var previous = orderedTeams[index - 1];
                var sharesRank =
                    current.TotalScore == previous.TotalScore &&
                    (!current.ResolutionTime.IsComparable ||
                     !previous.ResolutionTime.IsComparable ||
                     current.ResolutionTime == previous.ResolutionTime);

                if (sharesRank)
                {
                    position = ranked[index - 1].Position;
                }
            }

            ranked.Add((current.TeamId, position, current.TotalScore, current.ResolutionTime));
        }

        return ranked;
    }
}
