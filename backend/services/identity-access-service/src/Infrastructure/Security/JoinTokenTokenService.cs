using System.Security.Cryptography;
using System.Text;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Infrastructure.Security;

/// <summary>
/// Generates high-entropy join tokens and hashes them deterministically.
/// Issuance stores <see cref="HashToken"/> of the freshly generated plaintext, and
/// validation recomputes the hash of a supplied token to look the row up — so the hash
/// must be deterministic (no per-call salt). The plaintext token carries 256 bits of
/// randomness, which makes a plain SHA-256 digest sufficient against brute force.
/// </summary>
internal sealed class JoinTokenTokenService : IJoinTokenTokenService
{
    private const int TokenSizeInBytes = 32; // 256 bits of entropy.

    public string GenerateToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);
        return Base64UrlEncode(tokenBytes);
    }

    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
