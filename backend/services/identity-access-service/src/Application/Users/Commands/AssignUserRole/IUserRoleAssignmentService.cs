namespace umbral_backend.Application.Users.Commands.AssignUserRole;

public interface IUserRoleAssignmentService
{
    Task AssignAsync(AssignUserRoleCommand command, CancellationToken cancellationToken);
}
