using umbral_backend.Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace umbral_backend.Application.UnitTests.Common.Behaviours;

public class UnhandledExceptionBehaviourTests
{
    [Fact]
    public async Task Handle_WhenNoException_CallsNext()
    {
        var logger = NullLogger<SampleRequest>.Instance;
        var sut = new UnhandledExceptionBehaviour<SampleRequest, int>(logger);
        var result = await sut.Handle(new SampleRequest(), () => Task.FromResult(42), CancellationToken.None);

        result.Should().Be(42);
    }

    [Fact]
    public async Task Handle_WhenException_Rethrows()
    {
        var logger = NullLogger<SampleRequest>.Instance;
        var sut = new UnhandledExceptionBehaviour<SampleRequest, int>(logger);
        var exception = new InvalidOperationException("test failure");

        var act = () => sut.Handle(new SampleRequest(), () => throw exception, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed record SampleRequest : IRequest<int>;
}
