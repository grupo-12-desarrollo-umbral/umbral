namespace umbral_backend.Application.Common.Interfaces;

public interface IJoinTokenTokenService
{
    string GenerateToken();

    string HashToken(string token);
}
