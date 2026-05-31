namespace umbral_backend.Application.Common.Interfaces;

public interface INotifier
{
    Task NotifyAsync(string userId, string message, CancellationToken cancellationToken = default);
}
