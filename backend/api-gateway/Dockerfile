FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY src/ ./src/
WORKDIR /src/src
RUN dotnet restore ApiGateway.csproj
RUN dotnet publish ApiGateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 8080

# Bind to the platform-assigned $PORT (Railway et al.) with an 8080 fallback, while still
# deferring to an explicitly set ASPNETCORE_URLS — which is what docker-compose provides
# locally (http://+:8080), so local/compose/test behaviour is unchanged. Keep `+` (binds
# IPv4 and IPv6): Railway's private network is IPv6-only.
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=\"${ASPNETCORE_URLS:-http://+:${PORT:-8080}}\"; exec dotnet ApiGateway.dll"]
