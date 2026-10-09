using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Identity;

namespace Promotora.Application.Identity.Registration;

public sealed record CrearUsuarioCommand(string Email, string Password, string Nombre, string Apellido,
    RolUsuario Rol, PerfilNuevo Perfil) : ICommand<UsuarioDto>
{
    public override string ToString() => nameof(CrearUsuarioCommand);
}
