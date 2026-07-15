using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using umbral_backend.Api.Controllers;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Dtos.Scores;
using umbral_backend.Application.Scores.Commands.ApplyPenalty;
using umbral_backend.Application.Scores.Common.Authorization;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Controllers;

public sealed class PenaltiesControllerTests
{
    private readonly Mock<ISender> _sender;
    private readonly Mock<IScoringSessionAccessResolver> _accessResolver;
    private readonly PenaltiesController _controller;

    public PenaltiesControllerTests()
    {
        _sender = new Mock<ISender>();
        _accessResolver = new Mock<IScoringSessionAccessResolver>();
        _controller = new PenaltiesController(_sender.Object, _accessResolver.Object);
    }

    [Fact]
    public async Task ApplyPenaltyAsync_WhenValid_ReturnsCreatedWithAppliedPenaltyDto()
    {
        var liveSessionId = Guid.NewGuid();
        var command = new ApplyPenaltyCommand(Guid.Empty, Guid.NewGuid(), "Late arrival");
        var expectedResult = new AppliedPenaltyDto(
            Guid.NewGuid(),
            command.TeamId,
            100,
            command.Reason,
            DateTimeOffset.UtcNow);

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sender
            .Setup(s => s.Send(
                It.Is<ApplyPenaltyCommand>(c =>
                    c.LiveSessionId == liveSessionId &&
                    c.TeamId == command.TeamId &&
                    c.Reason == command.Reason),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await _controller.ApplyPenaltyAsync(
            liveSessionId, command, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(PenaltiesController.ApplyPenaltyAsync));
        createdResult.RouteValues.Should().ContainKey("liveSessionId")
            .WhoseValue.Should().Be(liveSessionId);

        var response = createdResult.Value.Should().BeOfType<AppliedPenaltyDto>().Subject;
        response.ScoreEntryId.Should().Be(expectedResult.ScoreEntryId);
        response.TeamId.Should().Be(expectedResult.TeamId);
        response.PenaltyAmount.Should().Be(expectedResult.PenaltyAmount);
        response.Reason.Should().Be(expectedResult.Reason);
        response.AppliedAt.Should().Be(expectedResult.AppliedAt);
    }

    [Fact]
    public async Task ApplyPenaltyAsync_WhenAccessDenied_PropagatesForbiddenAccessException()
    {
        var liveSessionId = Guid.NewGuid();
        var command = new ApplyPenaltyCommand(Guid.Empty, Guid.NewGuid(), "Late arrival");

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        var act = () => _controller.ApplyPenaltyAsync(
            liveSessionId, command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ApplyPenaltyAsync_WhenAccessDenied_DoesNotDispatchToMediatR()
    {
        var liveSessionId = Guid.NewGuid();
        var command = new ApplyPenaltyCommand(Guid.Empty, Guid.NewGuid(), "Late arrival");

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        try
        {
            await _controller.ApplyPenaltyAsync(liveSessionId, command, CancellationToken.None);
        }
        catch (ForbiddenAccessException)
        {
        }

        _sender.Verify(
            s => s.Send(It.IsAny<ApplyPenaltyCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyPenaltyAsync_WhenBlankReason_PropagatesValidationException()
    {
        var liveSessionId = Guid.NewGuid();
        var command = new ApplyPenaltyCommand(Guid.Empty, Guid.NewGuid(), string.Empty);
        var validationException = new ValidationException(
            new[] { new ValidationFailure("Reason", "Reason must not be empty.") });

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sender
            .Setup(s => s.Send(
                It.Is<ApplyPenaltyCommand>(c =>
                    c.LiveSessionId == liveSessionId &&
                    string.IsNullOrEmpty(c.Reason)),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(validationException);

        var act = () => _controller.ApplyPenaltyAsync(
            liveSessionId, command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ApplyPenaltyAsync_WhenMissingRequiredFields_PropagatesValidationException()
    {
        var liveSessionId = Guid.NewGuid();
        var command = new ApplyPenaltyCommand(Guid.Empty, Guid.Empty, string.Empty);
        var validationException = new ValidationException(
            new[]
            {
                new ValidationFailure("TeamId", "TeamId must not be empty."),
                new ValidationFailure("Reason", "Reason must not be empty.")
            });

        _accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sender
            .Setup(s => s.Send(
                It.Is<ApplyPenaltyCommand>(c =>
                    c.LiveSessionId == liveSessionId &&
                    c.TeamId == Guid.Empty &&
                    string.IsNullOrEmpty(c.Reason)),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(validationException);

        var act = () => _controller.ApplyPenaltyAsync(
            liveSessionId, command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
