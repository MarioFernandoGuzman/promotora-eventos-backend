using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed class CambiarPasswordHandler(IUsuarioRepository usuarios, ISesionRepository sesiones, IUsuarioActual actual,
    UsuarioAccess access, IPasswordService passwords, IAuditLogger audit) : ICommandHandler<CambiarPasswordCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(CambiarPasswordCommand request, CancellationToken ct) => sesiones.ExecuteAtomicAsync(async token =>
    {
        await access.LockAsync(request.Id, token);
        var actor = await access.ActorAsync(token);
        if (actor is null || !actor.PuedeCambiarPassword(request.Id))
        {
            return Result<Unit>.Failure(UsuarioAccess.Forbidden);
        }

        if (!passwords.Verify(actor.PasswordHash, request.PasswordActual))
        {
            await audit.WriteAsync(actual.UsuarioId, "LOGIN_FAILED", "usuarios", actor.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen,
                new DetalleAuditoria(MotivoAuditoria.CambioPasswordRechazado), token);
            return Result<Unit>.Failure(new Error("identity.password_invalid", "La contraseña actual es incorrecta.", ErrorType.Unauthorized));
        }

        if (passwords.Verify(actor.PasswordHash, request.PasswordNuevo))
        {
            return Result<Unit>.Failure(new Error("identity.password_reused", "Elige una contraseña diferente.", ErrorType.BusinessRule));
        }

        await usuarios.ChangePasswordAsync(actor.Id, passwords.Hash(request.PasswordNuevo), token);
        await sesiones.RevokeUserAsync(actor.Id, token);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "usuarios", actor.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen,
            new DetalleAuditoria(MotivoAuditoria.CambioPassword), token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
