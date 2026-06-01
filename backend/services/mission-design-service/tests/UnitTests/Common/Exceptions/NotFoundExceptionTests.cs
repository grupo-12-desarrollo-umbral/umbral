using umbral_backend.Application.Common.Exceptions;

namespace umbral_backend.Application.UnitTests.Common.Exceptions;

public class NotFoundExceptionTests
{
    [Fact]
    public void ConstructorWithNameAndKey_FormatsMessage()
    {
        var exception = new NotFoundException("Mission", 99);

        exception.Message.Should().Be("Entity \"Mission\" (99) was not found.");
    }

    [Fact]
    public void ConstructorWithMessage_SetsMessage()
    {
        var exception = new NotFoundException("Something was not found.");

        exception.Message.Should().Be("Something was not found.");
    }
}
