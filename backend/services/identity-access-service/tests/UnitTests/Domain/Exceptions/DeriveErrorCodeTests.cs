using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Exceptions;

// Direct coverage for DomainException.DeriveErrorCode edge branches that real exception names (all
// PascalCase, all ending in "Exception") never reach: a non-"Exception" suffix, the exact string
// "Exception", and a name whose first character is lowercase (IsUpper false at i == 0).
public sealed class DeriveErrorCodeTests
{
    [Theory]
    [InlineData("TeamNotActiveException", "team-not-active")]
    [InlineData("PlainName", "plain-name")]        // does not end in "Exception" → suffix branch not taken
    [InlineData("Exception", "exception")]          // length == suffix length → suffix NOT stripped
    [InlineData("aBc", "a-bc")]                    // lowercase first char → IsUpper false at i == 0
    [InlineData("A", "a")]                          // single char → i > 0 never true
    public void DeriveErrorCode_ProducesKebabCase(string typeName, string expected)
    {
        DomainException.DeriveErrorCode(typeName).Should().Be(expected);
    }
}
