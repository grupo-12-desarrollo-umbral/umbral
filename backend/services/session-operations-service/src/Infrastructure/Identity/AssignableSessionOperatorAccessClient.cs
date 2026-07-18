using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Identity;

/// <summary>
/// HTTP adapter that resolves assignable operator facts from the
/// users-service user catalog. External integration glue.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AssignableSessionOperatorAccessClient : IAssignableSessionOperatorAccessClient
{
    private const int PageSize = 100;
    private const string Source = "users-service.user-catalog";

    private readonly HttpClient _httpClient;
    private readonly ICurrentUser _currentUser;

    public AssignableSessionOperatorAccessClient(HttpClient httpClient, ICurrentUser currentUser)
    {
        _httpClient = httpClient;
        _currentUser = currentUser;
    }

    public async Task<SessionOperatorEligibilityDecisionDto> GetEligibilityAsync(
        int operatorUserId,
        CancellationToken cancellationToken)
    {
        var currentPage = 1;

        while (true)
        {
            var response = await GetUserCatalogPageAsync(currentPage, cancellationToken);
            var matchingUser = response.Items.SingleOrDefault(item => item.Id == operatorUserId);

            if (matchingUser is not null)
            {
                return BuildDecision(matchingUser, operatorUserId);
            }

            if (response.TotalCount <= currentPage * response.PageSize)
            {
                return new SessionOperatorEligibilityDecisionDto(
                    Source,
                    false,
                    operatorUserId,
                    null,
                    "User was not found in the identity access catalog.");
            }

            currentPage++;
        }
    }

    private async Task<UserCatalogPageResponse> GetUserCatalogPageAsync(int page, CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/users?page={page}&pageSize={PageSize}");

        ForwardTrustedHeaders(requestMessage);

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UserCatalogPageResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Users-service returned an empty user catalog response.");
    }

    private void ForwardTrustedHeaders(HttpRequestMessage requestMessage)
    {
        AddHeaderIfPresent(requestMessage, "X-User-Id", _currentUser.Id);
        AddHeaderIfPresent(requestMessage, "X-User-Role", _currentUser.Role);
        AddHeaderIfPresent(requestMessage, "X-User-Email", _currentUser.Email);
    }

    private static void AddHeaderIfPresent(HttpRequestMessage requestMessage, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            requestMessage.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private static SessionOperatorEligibilityDecisionDto BuildDecision(UserCatalogItemResponse user, int operatorUserId)
    {
        var isEligibleRole = string.Equals(user.Role, "Operator", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "Administrator", StringComparison.OrdinalIgnoreCase);

        var isEligible = user.IsActive && isEligibleRole;
        var reason = isEligible
            ? null
            : user.IsActive
                ? $"User role '{user.Role}' is not assignable as a session operator."
                : "User is deactivated in the identity access catalog.";

        return new SessionOperatorEligibilityDecisionDto(
            Source,
            isEligible,
            operatorUserId,
            user.Role,
            reason,
            user.ExternalIdentityId);
    }

    private sealed record UserCatalogPageResponse(
        IReadOnlyList<UserCatalogItemResponse> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record UserCatalogItemResponse(
        int Id,
        string ExternalIdentityId,
        string DisplayName,
        string Email,
        string Role,
        bool IsActive);
}
