using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Web.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
