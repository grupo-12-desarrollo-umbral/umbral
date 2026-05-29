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

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
