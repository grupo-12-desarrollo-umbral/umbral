var builder = WebApplication.CreateBuilder(args);
builder.AddGatewayServices();

var app = builder.Build();
app.UseCors(DependencyInjection.FrontendCorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.Run();
