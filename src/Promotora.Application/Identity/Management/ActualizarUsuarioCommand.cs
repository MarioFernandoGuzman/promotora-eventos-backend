using Promotora.Application.Identity.Registration;
using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Identity.Management;

public sealed record ActualizarUsuarioCommand(int Id, string Email, string Nombre, string Apellido, PerfilNuevo Perfil) : ICommand<UsuarioDetalleDto>;
