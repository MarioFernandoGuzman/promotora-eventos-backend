using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed class ActualizarUsuarioHandler(IUsuarioRepository usuarios, ISesionRepository sesiones,
    IUsuarioActual actual, UsuarioAccess access, IAuditLogger audit) : ICommandHandler<ActualizarUsuarioCommand, UsuarioDetalleDto>
{
    public async Task<Result<UsuarioDetalleDto>> HandleAsync(ActualizarUsuarioCommand request, CancellationToken ct)
    {
        try
        {
            return await sesiones.ExecuteAtomicAsync(async token =>
            {
                await access.LockAsync(request.Id, token);
                var actor = await access.ActorAsync(token);
                if (actor is null || !actor.PuedeGestionarUsuario(request.Id))
                {
                    return Result<UsuarioDetalleDto>.Failure(UsuarioAccess.Forbidden);
                }

                var target = await usuarios.FindByIdAsync(request.Id, token);
                if (target is null)
                {
                    return Result<UsuarioDetalleDto>.Failure(UsuarioAccess.NotFound);
                }

                if (target.Rol == RolUsuario.DUENO_RECINTO && string.IsNullOrWhiteSpace(request.Perfil.Empresa))
                {
                    return Result<UsuarioDetalleDto>.Failure(new Error("identity.empresa", "El dueño requiere una empresa.", ErrorType.BusinessRule));
                }

                var email = request.Email.Trim().ToLowerInvariant();
                if (email != target.Email && await usuarios.FindByEmailAsync(email, token) is not null)
                {
                    return Result<UsuarioDetalleDto>.Failure(new Error("identity.email_duplicate", "El correo ya está registrado.", ErrorType.Conflict));
                }

                await usuarios.UpdateAsync(request.Id, email, request.Nombre.Trim(), request.Apellido.Trim(), request.Perfil, token);
                if (email != target.Email)
                {
                    await sesiones.RevokeUserAsync(request.Id, token);
                }

                await audit.WriteAsync(actual.UsuarioId, "UPDATE", "usuarios", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen,
                    new DetalleAuditoria(MotivoAuditoria.EdicionUsuario), token);
                return Result<UsuarioDetalleDto>.Success((await usuarios.GetDetailAsync(request.Id, token))!);
            }, ct);
        }
        catch (UsuarioEmailDuplicadoException)
        {
            return Result<UsuarioDetalleDto>.Failure(new Error("identity.email_duplicate", "El correo ya está registrado.", ErrorType.Conflict));
        }
    }
}
