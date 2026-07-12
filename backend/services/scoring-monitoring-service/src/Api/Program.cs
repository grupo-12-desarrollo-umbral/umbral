using System.Diagnostics.CodeAnalysis;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();

[ExcludeFromCodeCoverage]
public partial class Program;
