using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Scores.Common.Authorization;

public sealed class ScoringSessionAuthorizationProxy : IScoringSessionAccessResolver
{
    private const string AdministratorRole = "Administrator";
    private const string OperatorRole = "Operator";

    private readonly ICurrentUser _currentUser;
    private readonly ISessionAssignmentReadRepository _assignmentReadRepository;

    public ScoringSessionAuthorizationProxy(
        ICurrentUser currentUser,
        ISessionAssignmentReadRepository assignmentReadRepository)
    {
        _currentUser = currentUser;
        _assignmentReadRepository = assignmentReadRepository;
    }

    public async Task EnsureAccessAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        if (string.Equals(_currentUser.Role, AdministratorRole, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(_currentUser.Role, OperatorRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException();
        }

        var assignedOperatorUserId = await _assignmentReadRepository
            .GetAssignedOperatorUserIdAsync(liveSessionId, cancellationToken);

        if (assignedOperatorUserId is null)
        {
            throw new ForbiddenAccessException();
        }

        if (!Guid.TryParse(_currentUser.Id, out var currentUserId)
            || assignedOperatorUserId.Value != currentUserId)
        {
            throw new ForbiddenAccessException();
        }
    }
}
