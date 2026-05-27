using umbral_backend.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddKeyVaultIfConfigured();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(static builder =>
    builder.AllowAnyMethod()
        .AllowAnyHeader()
        .AllowAnyOrigin());

app.MapOpenApi();

app.UseExceptionHandler(options => { });

app.MapEndpoints(typeof(Program).Assembly);

app.Run();
