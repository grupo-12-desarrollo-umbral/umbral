using System.Reflection;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.UnitTests.Common.Security;

public sealed class QueryAuthorizationTests
{
    [Fact]
    public void MissionAndTriviaQueries_RequireAdministratorOrOperatorRole()
    {
        var applicationAssembly = typeof(GetMissionCatalogQuery).Assembly;
        var queryTypes = applicationAssembly.GetTypes()
            .Where(type =>
                type.Name.EndsWith("Query", StringComparison.Ordinal) &&
                (type.Namespace?.StartsWith(
                    "umbral_backend.Application.Missions.Queries",
                    StringComparison.Ordinal) == true ||
                 type.Namespace?.StartsWith(
                    "umbral_backend.Application.Trivias.Queries",
                    StringComparison.Ordinal) == true))
            .ToArray();

        queryTypes.Should().HaveCount(7);

        foreach (var queryType in queryTypes)
        {
            var authorize = queryType.GetCustomAttribute<AuthorizeAttribute>();
            authorize.Should().NotBeNull($"{queryType.FullName} exposes mission-design read data");
            authorize!.Roles.Split(',').Should().BeEquivalentTo(Roles.Administrator, Roles.Operator);
        }
    }
}
