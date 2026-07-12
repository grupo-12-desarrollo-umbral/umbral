using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public class GeoCoordinatesTests
{
    [Fact]
    public void Create_WithValidCoordinates_StoresLatitudeAndLongitude()
    {
        var coordinates = GeoCoordinates.Create(4.711, -74.0721);

        coordinates.Latitude.Should().Be(4.711);
        coordinates.Longitude.Should().Be(-74.0721);
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Create_AtRangeBoundaries_Succeeds(double latitude, double longitude)
    {
        var act = () => GeoCoordinates.Create(latitude, longitude);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    [InlineData(1000)]
    public void Create_WhenLatitudeOutOfRange_Throws(double latitude)
    {
        var act = () => GeoCoordinates.Create(latitude, 0);

        act.Should().Throw<TargetLatitudeOutOfRangeException>();
    }

    [Theory]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    [InlineData(1000)]
    public void Create_WhenLongitudeOutOfRange_Throws(double longitude)
    {
        var act = () => GeoCoordinates.Create(0, longitude);

        act.Should().Throw<TargetLongitudeOutOfRangeException>();
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        GeoCoordinates.Create(4.711, -74.0721).Should().Be(GeoCoordinates.Create(4.711, -74.0721));
        GeoCoordinates.Create(4.711, -74.0721).Should().NotBe(GeoCoordinates.Create(4.712, -74.0721));
    }
}
