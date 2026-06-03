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
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
app.MapEndpoints(typeof(Program).Assembly);
app.MapHub<SessionsHub>("/hubs/sessions")
    .RequireAuthorization(AuthorizationPolicies.Participant);

app.Run();

[ExcludeFromCodeCoverage]
public partial class Program
{
}
