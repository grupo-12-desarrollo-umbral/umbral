namespace umbral_backend.Application.Common.Interfaces;

public interface IDatabaseHealthCheck
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
