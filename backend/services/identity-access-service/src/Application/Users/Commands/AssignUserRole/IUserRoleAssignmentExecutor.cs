namespace umbral_backend.Application.Users.Commands.AssignUserRole;

public interface IUserRoleAssignmentExecutor
{
    Task AssignAsync(AssignUserRoleCommand command, CancellationToken cancellationToken);
}
