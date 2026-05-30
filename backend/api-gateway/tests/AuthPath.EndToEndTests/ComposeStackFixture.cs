namespace ApiGateway.AuthPath.EndToEndTests;

public sealed class ComposeStackFixture : IAsyncLifetime
{
    private static readonly Uri GatewayBaseAddress = ReadUriFromEnvironment("UMBRAL_GATEWAY_BASE_URL", "http://127.0.0.1:8000");
    private static readonly Uri KeycloakBaseAddress = ReadUriFromEnvironment("UMBRAL_KEYCLOAK_BASE_URL", "http://127.0.0.1:8080");
    private static readonly Uri ProbeBaseAddress = ReadUriFromEnvironment("UMBRAL_PROBE_BASE_URL", "http://127.0.0.1:18081");
    private readonly HttpClient _probeClient = CreateHttpClient(ProbeBaseAddress);
    private readonly HttpClient _keycloakClient = CreateHttpClient(KeycloakBaseAddress);

    public HttpClient GatewayClient { get; } = CreateHttpClient(GatewayBaseAddress);

    public async Task InitializeAsync()
    {
        await RunDockerComposeIgnoreFailuresAsync("down", "-v", "--remove-orphans");
        await RunDockerComposeAsync("up", "-d", "--build");

        await WaitUntilAsync(
            async () =>
            {
                using var response = await _probeClient.GetAsync("/probe/health");
                return response.IsSuccessStatusCode;
            },
            "auth probe to become healthy");

        await WaitUntilAsync(
            async () =>
            {
                using var response = await GatewayClient.GetAsync("/api/test-auth-probe/inspect");
                return response.StatusCode == HttpStatusCode.Unauthorized;
            },
            "gateway auth route to reject anonymous requests");

        await WaitUntilAsync(
            async () =>
            {
                using var response = await _keycloakClient.GetAsync("/realms/umbral/.well-known/openid-configuration");
                return response.IsSuccessStatusCode;
            },
            "Keycloak realm metadata to be available");
    }

    public async Task DisposeAsync()
    {
        GatewayClient.Dispose();
        _probeClient.Dispose();
        _keycloakClient.Dispose();

        await RunDockerComposeAsync("down", "-v", "--remove-orphans");
    }

    public async Task ResetProbeAsync()
    {
        using var response = await _probeClient.PostAsync("/probe/reset", content: null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ProbeStateResponse> GetProbeStateAsync()
    {
        var state = await _probeClient.GetFromJsonAsync<ProbeStateResponse>("/probe/state");
        return state ?? throw new InvalidOperationException("Probe state response was empty.");
    }

    public async Task<(string AccessToken, string Subject)> GetAdminAccessTokenAsync()
    {
        using var response = await _keycloakClient.PostAsync(
            "/realms/umbral/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "umbral-web",
                ["username"] = "admin",
                ["password"] = "admin123"
            }));

        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        if (token?.AccessToken is null)
        {
            throw new InvalidOperationException("Keycloak token response did not include an access token.");
        }

        return (token.AccessToken, ReadSubject(token.AccessToken));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string description)
    {
        var timeout = TimeSpan.FromMinutes(2);
        var pollInterval = TimeSpan.FromSeconds(2);
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            try
            {
                if (await condition())
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(pollInterval);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private static async Task RunDockerComposeAsync(params string[] args)
    {
        var (exitCode, standardOutput, standardError, composeArguments) = await RunDockerComposeProcessAsync(args);
        if (exitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"docker compose {string.Join(' ', composeArguments)} failed with exit code {exitCode}.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{standardError}");
    }

    private static async Task RunDockerComposeIgnoreFailuresAsync(params string[] args)
    {
        await RunDockerComposeProcessAsync(args);
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError, IReadOnlyList<string> ComposeArguments)> RunDockerComposeProcessAsync(params string[] args)
    {
        var repositoryRoot = FindRepositoryRoot();
        var composeArguments = new List<string>
        {
            "compose",
            "-f",
            "docker-compose.yml",
            "-f",
            "api-gateway/tests/docker-compose.auth-tests.yml"
        };

        composeArguments.AddRange(args);

        var startInfo = new ProcessStartInfo("docker")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in composeArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker compose.");

        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, standardOutput, standardError, composeArguments);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "docker-compose.yml")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }

    private static HttpClient CreateHttpClient(Uri baseAddress)
    {
        return new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    private static Uri ReadUriFromEnvironment(string variableName, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return new Uri(string.IsNullOrWhiteSpace(value) ? fallback : value, UriKind.Absolute);
    }

    private static string ReadSubject(string jwt)
    {
        var segments = jwt.Split('.');
        if (segments.Length < 2)
        {
            throw new InvalidOperationException("JWT did not contain a payload segment.");
        }

        var payload = segments[1]
            .Replace('-', '+')
            .Replace('_', '/');

        var padding = 4 - (payload.Length % 4);
        if (padding is > 0 and < 4)
        {
            payload = payload.PadRight(payload.Length + padding, '=');
        }

        var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        if (!json.RootElement.TryGetProperty("sub", out var subjectElement))
        {
            throw new InvalidOperationException("JWT payload did not contain a sub claim.");
        }

        return subjectElement.GetString()
            ?? throw new InvalidOperationException("JWT sub claim was null.");
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}

public sealed record ProbeStateResponse(int HitCount);
