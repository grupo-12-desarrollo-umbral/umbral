using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Security;

public static class GatewayRoleParser
{
    public static bool TryParse(string? value, out Role role)
    {
        role = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim() switch
        {
            "Administrator" or "Administrador" => Assign(Role.Administrator, out role),
            "Operator" or "Operador" => Assign(Role.Operator, out role),
            "Participant" or "Participante" => Assign(Role.Participant, out role),
            _ => false
        };
    }

    public static Role Parse(string value)
    {
        if (!TryParse(value, out var role))
        {
            throw new ValidationException($"Unsupported role '{value}'.");
        }

        return role;
    }

    private static bool Assign(Role parsedRole, out Role role)
    {
        role = parsedRole;
        return true;
    }
}
