var builder = WebApplication.CreateBuilder(args);
builder.AddGatewayServices();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.Run();
