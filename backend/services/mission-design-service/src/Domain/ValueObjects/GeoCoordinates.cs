using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

/// <summary>
/// The geographic location an operator assigns to a treasure-hunt <see cref="Entities.Target"/>.
/// Display/context metadata only: it is carried through to participant runtime play so the mobile
/// app can render the target on a map, but QR validation remains the sole source of truth for
/// target resolution. No geofencing or GPS proximity enforcement is implied.
/// </summary>
public sealed class GeoCoordinates : ValueObject
{
    public const double MinimumLatitude = -90;
    public const double MaximumLatitude = 90;
    public const double MinimumLongitude = -180;
    public const double MaximumLongitude = 180;

    private GeoCoordinates()
    {
    }

    private GeoCoordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public static GeoCoordinates Create(double latitude, double longitude)
    {
        if (latitude < MinimumLatitude || latitude > MaximumLatitude)
        {
            throw new TargetLatitudeOutOfRangeException();
        }

        if (longitude < MinimumLongitude || longitude > MaximumLongitude)
        {
            throw new TargetLongitudeOutOfRangeException();
        }

        return new GeoCoordinates(latitude, longitude);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}
