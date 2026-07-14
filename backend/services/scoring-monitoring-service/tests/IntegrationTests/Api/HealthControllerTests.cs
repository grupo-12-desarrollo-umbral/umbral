using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using umbral_backend.Api.Controllers;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class HealthControllerTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseCanConnect_ReturnsHealthy()
    {
        var healthCheck = new Mock<IDatabaseHealthCheck>();
        healthCheck.Setup(check => check.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = new HealthController();

        var result = await controller.CheckHealthAsync(healthCheck.Object, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseCannotConnect_ReturnsServiceUnavailableProblem()
    {
        var healthCheck = new Mock<IDatabaseHealthCheck>();
        healthCheck.Setup(check => check.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = new HealthController();

        var result = await controller.CheckHealthAsync(healthCheck.Object, CancellationToken.None);

        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void Alive_ReturnsOk()
    {
        new HealthController().Alive().Should().BeOfType<OkObjectResult>();
    }
}
