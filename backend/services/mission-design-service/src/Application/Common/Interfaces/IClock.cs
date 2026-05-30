namespace umbral_backend.Application.Common.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
