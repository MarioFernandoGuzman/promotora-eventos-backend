using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Sessions;

public sealed class LogoutHandler(ISesionRepository sesiones, ITokenService tokens, IUsuarioActual actual, IAuditLogger audit)
    : ICommandHandler<LogoutCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(LogoutCommand request, CancellationToken ct) => sesiones.ExecuteAtomicAsync(async token =>
    {
        var refresh = await sesiones.FindAsync(tokens.HashRefreshToken(request.RefreshToken), token);
        if (refresh is null || refresh.UsuarioId != actual.UsuarioId)
        {
            return Result<Unit>.Failure(SesionErrors.Invalid);
        }

        await sesiones.LockUsuarioAsync(refresh.UsuarioId, token);
        await sesiones.RevokeFamilyAsync(refresh.FamiliaId, token);
        await audit.WriteAsync(refresh.UsuarioId, "LOGOUT", "usuarios", refresh.UsuarioId.ToString(CultureInfo.InvariantCulture), actual.IpOrigen,
            new DetalleAuditoria(MotivoAuditoria.LogoutSolicitado, refresh.FamiliaId), token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
