using System.Runtime.CompilerServices;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Exceptions;

/// <summary>
/// Golden-file contract pinning every concrete <see cref="DomainException"/> to its public error code.
/// The code surfaces as the RFC 7807 <c>type</c> (ProblemDetails), so a class rename or a changed
/// <c>ErrorCode</c> override is a breaking API change. This test fails loudly instead of letting that
/// ship silently.
/// </summary>
public sealed class ErrorCodeContractTests
{
    // Expected class name -> public ErrorCode. Deliberately hand-pinned (NOT derived) so a rename that
    // changes the derived code is caught. Adding a new DomainException subclass is a one-line addition
    // here; removing/renaming one requires a deliberate edit — that is the whole point of the contract.
    private static readonly IReadOnlyDictionary<string, string> ExpectedErrorCodes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DeactivatedUserAccessDeniedException"] = "deactivated-user-access-denied",
            ["DeactivatedUserRoleAssignmentNotAllowedException"] = "deactivated-user-role-assignment-not-allowed",
            ["ExternalIdentityIdRequiredException"] = "external-identity-id-required",
            ["ExternalIdentityMismatchException"] = "external-identity-mismatch",
            ["IdentityProviderNameRequiredException"] = "identity-provider-name-required",
            ["IdentityProviderRoleSyncException"] = "identity-provider-role-sync",
            ["IdentityProviderUserStateSyncException"] = "identity-provider-user-state-sync",
            ["InvitedEmailAlreadyRegisteredException"] = "invited-email-already-registered",
            ["ParticipantAlreadyAuthorizedForTeamException"] = "participant-already-authorized-for-team",
            ["ParticipantNotInvitableException"] = "participant-not-invitable",
            ["TeamAlreadyDeactivatedException"] = "team-already-deactivated",
            ["TeamCodeAlreadyExistsException"] = "team-code-already-exists",
            ["TeamCodeRequiredException"] = "team-code-required",
            ["TeamDisplayNameRequiredException"] = "team-display-name-required",
            ["TeamNotActiveException"] = "team-not-active",
            ["UserAccessAlreadyActiveException"] = "user-access-already-active",
            ["UserAccessAlreadyDeactivatedException"] = "user-access-already-deactivated",
            ["UserDisplayNameRequiredException"] = "user-display-name-required",
            ["UserEmailRequiredException"] = "user-email-required",
            ["UserRoleNotAuthorizedException"] = "user-role-not-authorized",
        };

    private static IReadOnlyDictionary<string, string> ActualErrorCodes()
    {
        var baseType = typeof(DomainException);
        return baseType.Assembly
            .GetTypes()
            .Where(type => baseType.IsAssignableFrom(type) && !type.IsAbstract)
            // Read ErrorCode without running a constructor: arities vary and no override reads ctor state
            // (none override ErrorCode here; the default reads only GetType().Name).
            .ToDictionary(
                type => type.Name,
                type => ((DomainException)RuntimeHelpers.GetUninitializedObject(type)).ErrorCode,
                StringComparer.Ordinal);
    }

    [Fact]
    public void EveryConcreteDomainException_MapsToItsPinnedErrorCode()
    {
        var actual = ActualErrorCodes();

        actual.Should().BeEquivalentTo(ExpectedErrorCodes);
    }

    [Fact]
    public void ErrorCodes_AreUniqueAcrossExceptions()
    {
        var actual = ActualErrorCodes();

        var duplicates = actual
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        duplicates.Should().BeEmpty("each DomainException must expose a distinct public error code");
    }
}
