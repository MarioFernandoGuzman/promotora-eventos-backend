using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed class BajaUsuarioHandler(IUsuarioRepository usuarios, ISesionRepository sesiones, IUsuarioActual actual,
    UsuarioAccess access, IAuditLogger audit) : ICommandHandler<BajaUsuarioCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(BajaUsuarioCommand request, CancellationToken ct) => sesiones.ExecuteAtomicAsync(async token =>
    {
        await access.LockAsync(request.Id, token);
        var actor = await access.ActorAsync(token);
        if (actor is null || !actor.PuedeGestionarUsuario(request.Id))
        {
            return Result<Unit>.Failure(UsuarioAccess.Forbidden);
        }

        var target = await usuarios.FindByIdAsync(request.Id, token);
        if (target is null)
        {
            return Result<Unit>.Failure(UsuarioAccess.NotFound);
        }

        if (target.Estado == "INACTIVO")
        {
            return Result<Unit>.Success(Unit.Value);
        }

        if (!await usuarios.DeactivateAsync(target.Id, target.Estado, token))
        {
            return Result<Unit>.Failure(UsuarioAccess.Conflict);
        }

        await sesiones.RevokeUserAsync(target.Id, token);
        await audit.WriteAsync(actual.UsuarioId, "DELETE", "usuarios", target.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
