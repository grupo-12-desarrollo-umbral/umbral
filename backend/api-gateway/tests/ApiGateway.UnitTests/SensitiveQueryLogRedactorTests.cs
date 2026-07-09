using ApiGateway.Logging;
using FluentAssertions;
using Xunit;

namespace ApiGateway.UnitTests;

// Guards the log-redaction seam that keeps SignalR's ?access_token JWT out of stdout: the key must
// survive, its value must not.
public sealed class SensitiveQueryLogRedactorTests
{
    [Fact]
    public void RedactsTokenValueWhilePreservingKey()
    {
        var redacted = SensitiveQueryLogRedactor.Redact(
            "/hubs/session?access_token=eyJhbGciOiJSUzI1NiJ9.payload.sig");

        redacted.Should().Be("/hubs/session?access_token=[REDACTED]");
    }

    [Fact]
    public void LeavesTextWithoutSensitiveParametersUntouched()
    {
        const string clean = "/api/missions?foo=bar&page=2";

        SensitiveQueryLogRedactor.Redact(clean).Should().Be(clean);
    }

    [Fact]
    public void RedactsTokenWhenItIsTheOnlyParameter()
    {
        SensitiveQueryLogRedactor.Redact("/hubs/session?access_token=eyJsecretsig")
            .Should().Be("/hubs/session?access_token=[REDACTED]");
    }

    [Fact]
    public void RedactsTokenInTheMiddleOfTheQueryWithoutTouchingNeighbours()
    {
        var redacted = SensitiveQueryLogRedactor.Redact(
            "/api/missions?foo=bar&access_token=eyJsecret&page=2");

        redacted.Should().Be("/api/missions?foo=bar&access_token=[REDACTED]&page=2");
    }

    [Fact]
    public void RedactsEverySensitiveKeyWhenSeveralAppear()
    {
        var redacted = SensitiveQueryLogRedactor.Redact(
            "http://svc/cb?code=abc&id_token=def&refresh_token=ghi&keep=me");

        redacted.Should().Be("http://svc/cb?code=[REDACTED]&id_token=[REDACTED]&refresh_token=[REDACTED]&keep=me");
    }

    [Fact]
    public void RedactionIsCaseInsensitiveAndKeepsTheOriginalKeyCasing()
    {
        SensitiveQueryLogRedactor.Redact("/hubs/session?Access_Token=eyJsecret")
            .Should().Be("/hubs/session?Access_Token=[REDACTED]");
    }

    [Fact]
    public void DoesNotRedactAKeyThatMerelyEndsWithASensitiveName()
    {
        const string lookAlike = "/api/missions?my_access_token=value";

        SensitiveQueryLogRedactor.Redact(lookAlike).Should().Be(lookAlike);
    }
}
