namespace umbral_backend.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? Id { get; }
    List<string>? Roles { get; }
}
