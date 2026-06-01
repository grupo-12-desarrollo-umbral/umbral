using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Services;

namespace Microsoft.Extensions.DependencyInjection;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IAuthenticatedUserLoginHandler, AuthenticatedUserLoginHandler>();
        builder.Services.AddScoped<IAuthenticatedUserLoginEntryPoint, AuthenticatedUserLoginProxy>();
        builder.Services.AddScoped<IdentityProvisioningPolicy>();
        builder.Services.AddScoped<AccessPolicy>();
        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();
    }
}
