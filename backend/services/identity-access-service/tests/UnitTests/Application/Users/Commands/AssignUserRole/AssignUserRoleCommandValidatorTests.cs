using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.AssignUserRole;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Users.Commands.AssignUserRole;

public sealed class AssignUserRoleCommandValidatorTests
{
    [Fact]
    public async Task Validate_AcceptsKnownRoleForActiveExistingUser()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Provision("kc-10", "Target User", "target@example.com", Role.Operator));

        var validator = new AssignUserRoleCommandValidator(repository.Object);

        var result = await validator.ValidateAsync(new AssignUserRoleCommand(10, "Administrator"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_RejectsUnknownRoleValue()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.Provision("kc-10", "Target User", "target@example.com", Role.Operator));

        var validator = new AssignUserRoleCommandValidator(repository.Object);

        var result = await validator.ValidateAsync(new AssignUserRoleCommand(10, "SuperAdmin"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(AssignUserRoleCommand.Role)
            && error.ErrorMessage == "Role must be a known Role value.");
    }

    [Fact]
    public async Task Validate_RejectsMissingTargetUser()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var validator = new AssignUserRoleCommandValidator(repository.Object);

        var result = await validator.ValidateAsync(new AssignUserRoleCommand(99, "Participant"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(AssignUserRoleCommand.UserId)
            && error.ErrorMessage == "Target user must exist.");
    }

    [Fact]
    public async Task Validate_RejectsDeactivatedTargetUser()
    {
        var targetUser = User.Provision("kc-22", "Inactive User", "inactive@example.com", Role.Participant);
        targetUser.DeactivateAccess();

        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(22, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var validator = new AssignUserRoleCommandValidator(repository.Object);

        var result = await validator.ValidateAsync(new AssignUserRoleCommand(22, "Operator"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(AssignUserRoleCommand.UserId)
            && error.ErrorMessage == "Target user must be active.");
    }
}
