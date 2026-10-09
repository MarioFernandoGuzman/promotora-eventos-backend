using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Registration;

public sealed class CrearUsuarioHandler(IUsuarioRepository repository, IPasswordService passwords,
    IUsuarioActual actual, IClock clock) : ICommandHandler<CrearUsuarioCommand, UsuarioDto>
{
    public async Task<Result<UsuarioDto>> HandleAsync(CrearUsuarioCommand request, CancellationToken ct)
    {
        if (request.Rol != RolUsuario.CLIENTE)
        {
            var actor = actual.UsuarioId is int id ? await repository.FindByIdAsync(id, ct) : null;
            if (actor is not { PuedeAutenticarse: true, Rol: RolUsuario.ADMINISTRADOR, DebeCambiarPassword: false })
            {
                return Result<UsuarioDto>.Failure(new Error("identity.forbidden", "Se requiere un administrador habilitado.", ErrorType.Forbidden));
            }
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var usuario = new Usuario(0, email, passwords.Hash(request.Password), request.Nombre.Trim(), request.Apellido.Trim(),
            request.Rol, "ACTIVO", false, clock.UtcNow);
        return await repository.CreateAsync(usuario, request.Perfil, actual.UsuarioId, actual.IpOrigen, false, ct);
    }
}
