namespace ApiGateway.Transforms;

public sealed class TrustedHeadersTransform : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            AddIfPresent(context, "X-User-Id", user.FindFirstValue(ClaimTypes.NameIdentifier));
            AddIfPresent(context, "X-User-Role", user.FindAll(ClaimTypes.Role).FirstOrDefault()?.Value);
            AddIfPresent(context, "X-User-Email", user.FindFirstValue(ClaimTypes.Email));
        }

        // Strip the original token; downstream services must not re-validate it.
        context.ProxyRequest.Headers.Remove("Authorization");

        return ValueTask.CompletedTask;
    }

    private static void AddIfPresent(RequestTransformContext context, string header, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            context.ProxyRequest.Headers.TryAddWithoutValidation(header, value);
        }
    }
}
