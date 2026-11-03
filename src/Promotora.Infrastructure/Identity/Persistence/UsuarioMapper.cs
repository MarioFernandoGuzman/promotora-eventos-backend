using Promotora.Application.Identity.Registration;
using Promotora.Domain.Identity;

namespace Promotora.Infrastructure.Identity.Persistence;

internal static class UsuarioMapper
{
    internal static Usuario ToDomain(this UsuarioRow row) => new(row.Id, row.Email, row.PasswordHash,
        row.Nombre, row.Apellido, Enum.Parse<RolUsuario>(row.Rol), row.Estado, row.DebeCambiarPassword, row.FechaRegistro);

    internal static UsuarioDto ToDto(this UsuarioRow row) => new(row.Id, row.Email, row.Nombre, row.Apellido,
        row.Rol, row.Estado, row.DebeCambiarPassword);
}
