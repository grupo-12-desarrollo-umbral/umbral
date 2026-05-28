# Plan: Scaffold api-gateway project

## Context

The grill session resolved that a YARP-based ASP.NET Core gateway is the single JWT validation boundary for all four microservices (ADR-0001). Before any Phase X.4 service work begins, the gateway project must exist and build. This plan scaffolds it as a lean, self-contained ASP.NET Core + YARP app under `api-gateway/src/`.

---

## Files to create

All files live under `/home/samu/Desktop/umbral/umbral-backend/api-gateway/`.

### Project files

**`src/Directory.Build.props`**
Mirrors the pattern from `services/*/src/Directory.Build.props`:
- `TargetFramework`: `net10.0`
- `ImplicitUsings`: enable
- `Nullable`: enable

**`src/Directory.Packages.props`**
Central package versions (same pattern as services):
```
ManagePackageVersionsCentrally = true
Yarp.ReverseProxy              2.3.0
Microsoft.AspNetCore.Authentication.JwtBearer  10.0.0
```

**`src/ApiGateway.csproj`**
Minimal web SDK project:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <AssemblyName>ApiGateway</AssemblyName>
    <RootNamespace>ApiGateway</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageReference Include="Yarp.ReverseProxy" />
  </ItemGroup>
</Project>
```

**`src/GlobalUsings.cs`**
```csharp
global using System.Security.Claims;
global using ApiGateway.Transforms;
global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Yarp.ReverseProxy.Transforms;
```

---

### Entry point

**`src/Program.cs`**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddGatewayServices();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.Run();
```

**`src/DependencyInjection.cs`**
Extension method `AddGatewayServices` on `IHostApplicationBuilder`:
1. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opts =>`:
   - `opts.Authority` = `config["Keycloak:Authority"]`
   - `opts.Audience` = `config["Keycloak:Audience"]`
   - `opts.RequireHttpsMetadata` = `!env.IsDevelopment()`
   - `opts.Events.OnMessageReceived` = `WebSocketTokenExtractionTransform.OnMessageReceived`
2. `AddAuthorization()`
3. `AddReverseProxy().LoadFromConfig(config.GetSection("ReverseProxy")).AddTransforms(ctx => ctx.RequestTransforms.Add(new TrustedHeadersTransform()))`

---

### Transforms

**`src/Transforms/WebSocketTokenExtractionTransform.cs`**
Static helper implementing the JWT `OnMessageReceived` hook (not a YARP transform — runs in the ASP.NET Core auth middleware, before YARP):
```csharp
public static class WebSocketTokenExtractionTransform
{
    public static Task OnMessageReceived(MessageReceivedContext ctx)
    {
        // Browser WS clients cannot set Authorization headers; SignalR passes token as ?access_token
        if (ctx.Request.Query.TryGetValue("access_token", out var token) &&
            ctx.Request.Headers.TryGetValue("Upgrade", out var upgrade) &&
            upgrade.ToString().Equals("websocket", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Token = token;
        }
        return Task.CompletedTask;
    }
}
```

**`src/Transforms/TrustedHeadersTransform.cs`**
YARP `RequestTransform` that runs after authentication. Injects trusted headers and strips the original JWT:
```csharp
public sealed class TrustedHeadersTransform : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext ctx)
    {
        var user = ctx.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            AddIfPresent(ctx, "X-User-Id",    user.FindFirstValue(ClaimTypes.NameIdentifier));
            AddIfPresent(ctx, "X-User-Role",  user.FindAll(ClaimTypes.Role).FirstOrDefault()?.Value);
            AddIfPresent(ctx, "X-User-Email", user.FindFirstValue(ClaimTypes.Email));
        }
        // Strip the original token — downstream services must not re-validate it
        ctx.ProxyRequest.Headers.Remove("Authorization");
        return ValueTask.CompletedTask;
    }

    private static void AddIfPresent(RequestTransformContext ctx, string header, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            ctx.ProxyRequest.Headers.TryAddWithoutValidation(header, value);
    }
}
```

---

### Configuration

**`src/appsettings.json`** — production/Docker addresses (service names from docker-compose):
```json
{
  "Keycloak": {
    "Authority": "http://keycloak:8080/realms/umbral",
    "Audience": "umbral-web"
  },
  "ReverseProxy": {
    "Routes": {
      "mission-design":    { "ClusterId": "mission-design",    "Match": { "Path": "/api/missions/{**catch-all}" },  "AuthorizationPolicy": "default" },
      "identity":          { "ClusterId": "identity",          "Match": { "Path": "/api/identity/{**catch-all}" },  "AuthorizationPolicy": "default" },
      "session-ops":       { "ClusterId": "session-ops",       "Match": { "Path": "/api/sessions/{**catch-all}" }, "AuthorizationPolicy": "default" },
      "session-ops-hubs":  { "ClusterId": "session-ops",       "Match": { "Path": "/hubs/{**catch-all}" },         "AuthorizationPolicy": "default" },
      "scoring":           { "ClusterId": "scoring",           "Match": { "Path": "/api/scoring/{**catch-all}" },  "AuthorizationPolicy": "default" }
    },
    "Clusters": {
      "mission-design": { "Destinations": { "d1": { "Address": "http://mission-design-service:8080/" } } },
      "identity":       { "Destinations": { "d1": { "Address": "http://identity-access-service:8080/" } } },
      "session-ops":    { "Destinations": { "d1": { "Address": "http://session-operations-service:8080/" } } },
      "scoring":        { "Destinations": { "d1": { "Address": "http://scoring-monitoring-service:8080/" } } }
    }
  }
}
```

**`src/appsettings.Development.json`** — overrides cluster addresses to localhost ports:
```json
{
  "Keycloak": { "Authority": "http://localhost:8080/realms/umbral" },
  "ReverseProxy": {
    "Clusters": {
      "mission-design": { "Destinations": { "d1": { "Address": "http://localhost:5001/" } } },
      "identity":       { "Destinations": { "d1": { "Address": "http://localhost:5002/" } } },
      "session-ops":    { "Destinations": { "d1": { "Address": "http://localhost:5003/" } } },
      "scoring":        { "Destinations": { "d1": { "Address": "http://localhost:5004/" } } }
    }
  }
}
```

---

## Out of scope for this task

**`services/*/src/Api/Services/CurrentUser.cs`** currently reads `ClaimTypes.NameIdentifier` from JWT claims. Per ADR-0001, it must read `X-User-Id` / `X-User-Role` headers instead. This is the backend-agent's responsibility during Phase X.4 of each service — **do not touch now**.

---

## Verification

```bash
dotnet build api-gateway/src/ApiGateway.csproj
# Expected: Build succeeded, 0 Error(s)
```
