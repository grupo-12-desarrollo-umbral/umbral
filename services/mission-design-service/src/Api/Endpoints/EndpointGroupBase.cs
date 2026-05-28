using System.Reflection;

namespace umbral_backend.Web.Endpoints;

public interface IEndpointGroup
{
    static abstract void Map(RouteGroupBuilder groupBuilder);
}

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app, Assembly assembly)
    {
        var endpointGroupTypes = assembly.GetExportedTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           typeof(IEndpointGroup).IsAssignableFrom(type));

        foreach (var endpointGroupType in endpointGroupTypes)
        {
            var groupName = endpointGroupType.Name.Replace("Endpoints", string.Empty, StringComparison.Ordinal);
            var groupBuilder = app.MapGroup("/");
            groupBuilder.WithGroupName(groupName);

            endpointGroupType.GetMethod(nameof(IEndpointGroup.Map), BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, [groupBuilder]);
        }

        return app;
    }
}
