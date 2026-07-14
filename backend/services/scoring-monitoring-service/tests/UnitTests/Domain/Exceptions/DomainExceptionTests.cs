using umbral_backend.Domain.Exceptions;

namespace umbral_backend.ScoringMonitoring.UnitTests.Domain.Exceptions;

// Exercises DomainException: the DeriveErrorCode suffix-stripping/word-boundary branches, the
// default derived ErrorCode and null PublicDetail, both constructors, and the abstract Category
// contract — through a minimal concrete subclass.
public sealed class DomainExceptionTests
{
    private sealed class SampleConflictException : DomainException
    {
        public SampleConflictException()
            : base("conflict")
        {
        }

        public SampleConflictException(Exception inner)
            : base("conflict", inner)
        {
        }

        public override ErrorCategory Category => ErrorCategory.Conflict;
    }

    [Fact]
    public void DefaultErrorCode_IsDerivedFromTypeName()
    {
        new SampleConflictException().ErrorCode.Should().Be("sample-conflict");
    }

    [Fact]
    public void DefaultPublicDetail_IsNull()
    {
        new SampleConflictException().PublicDetail.Should().BeNull();
    }

    [Fact]
    public void Category_IsDeclaredByConcreteType()
    {
        new SampleConflictException().Category.Should().Be(ErrorCategory.Conflict);
    }

    [Fact]
    public void InnerExceptionConstructor_PreservesCause()
    {
        var cause = new InvalidOperationException("root");

        var exception = new SampleConflictException(cause);

        exception.InnerException.Should().BeSameAs(cause);
        exception.Message.Should().Be("conflict");
    }

    [Fact]
    public void DeriveErrorCode_NameWithoutExceptionSuffix_IsNotStripped()
    {
        DomainException.DeriveErrorCode("TeamNotFound").Should().Be("team-not-found");
    }

    [Fact]
    public void DeriveErrorCode_NameEqualToSuffix_IsNotStripped()
    {
        DomainException.DeriveErrorCode("Exception").Should().Be("exception");
    }

    [Fact]
    public void DeriveErrorCode_SingleWord_HasNoBoundaryDash()
    {
        DomainException.DeriveErrorCode("TeamException").Should().Be("team");
    }
}
