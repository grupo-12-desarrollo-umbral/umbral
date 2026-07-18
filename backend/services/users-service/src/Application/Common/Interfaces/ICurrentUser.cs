namespace umbral_backend.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? Id { get; }

    string? Email { get; }

    string? Role { get; }
}
