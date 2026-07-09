using System.Text.RegularExpressions;

namespace ApiGateway.Logging;

/// <summary>
/// Redacts the values of sensitive query-string parameters from free-form log text. SignalR
/// delivers its bearer token as <c>?access_token</c> (ADR-0002), which the framework's request
/// logging and YARP's forwarder would otherwise echo to stdout verbatim, leaving a replayable JWT
/// in <c>docker logs</c> (and, once OTLP is wired, in an indexed store).
/// </summary>
public static partial class SensitiveQueryLogRedactor
{
    public const string Placeholder = "[REDACTED]";

    // The alternation is a small allowlist, not a regex zoo: only the parameters this gateway can
    // plausibly receive that carry a secret. Anchored on a preceding ? or & (via look-behind so it
    // is not consumed) to match a real query key and never a suffix such as my_access_token.
    // Case-insensitive so a variant key is caught too. The value runs until the next delimiter
    // (&, #, whitespace) or end of string.
    [GeneratedRegex(
        @"(?<=[?&])(access_token|id_token|refresh_token|code)=[^&#\s]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveParameter();

    /// <summary>
    /// Replaces the value of every sensitive parameter with <see cref="Placeholder"/>, preserving
    /// the key (and its original casing) so the request stays legible: <c>access_token=[REDACTED]</c>.
    /// </summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return SensitiveParameter().Replace(
            text,
            static match => $"{match.Groups[1].Value}={Placeholder}");
    }
}
