using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using umbral_backend.Api.Hubs;
using umbral_backend.Api.Services;
using umbral_backend.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ScoringMonitoringDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseExceptionHandler(options => { });
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapControllers();
app.MapHub<ScoringHub>("/hubs/scoring")
    .RequireAuthorization(AuthorizationPolicies.ParticipantOrOperator);

app.Run();

[ExcludeFromCodeCoverage]
public partial class Program
{
}
