using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Exceptions;

// Exercises the suffix-stripping and word-boundary branches of DomainException.DeriveErrorCode that the
// golden ErrorCodeContractTests (whose type names all end in a strippable "Exception") never reaches.
public sealed class DeriveErrorCodeBranchTests
{
    [Fact]
    public void DeriveErrorCode_NameWithoutExceptionSuffix_IsNotStripped()
    {
        // EndsWith("Exception") is false → the `&&` short-circuits and no suffix is removed.
        DomainException.DeriveErrorCode("TeamNotFound").Should().Be("team-not-found");
    }

    [Fact]
    public void DeriveErrorCode_NameEqualToSuffix_IsNotStripped()
    {
        // EndsWith is true but Length is not greater than the suffix → the second `&&` operand is false,
        // so the name is kept verbatim rather than stripped to an empty string.
        DomainException.DeriveErrorCode("Exception").Should().Be("exception");
    }

    [Fact]
    public void DeriveErrorCode_SingleWord_HasNoBoundaryDash()
    {
        // Upper-case first char at i == 0 → the `i > 0` boundary guard is false, so no leading dash.
        DomainException.DeriveErrorCode("TeamException").Should().Be("team");
    }
}
