using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Sessions;

internal static class SesionErrors
{
    internal static Error Invalid => new("identity.invalid_credentials", "Credenciales o sesión inválidas.", ErrorType.Unauthorized);
}
