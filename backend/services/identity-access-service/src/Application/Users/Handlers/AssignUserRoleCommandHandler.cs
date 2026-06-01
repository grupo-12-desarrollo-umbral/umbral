using umbral_backend.Application.Users.Commands.AssignUserRole;

namespace umbral_backend.Application.Users.Handlers;

public sealed class AssignUserRoleCommandHandler : IRequestHandler<AssignUserRoleCommand>
{
    private readonly IUserRoleAssignmentService _userRoleAssignmentService;

    public AssignUserRoleCommandHandler(IUserRoleAssignmentService userRoleAssignmentService)
    {
        _userRoleAssignmentService = userRoleAssignmentService;
    }

    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        await _userRoleAssignmentService.AssignAsync(request, cancellationToken);
    }
}
