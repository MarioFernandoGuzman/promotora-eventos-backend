using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed class BajaLocalidadHandler(ILocalidadRepository localidades, IVenuesTransaction transaction,
    LocalidadAccess access, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<BajaLocalidadCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(BajaLocalidadCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var recinto = await access.RecintoAsync(request.RecintoId, true, false, token);
        if (recinto.IsFailure)
        {
            return Result<Unit>.Failure(recinto.Errors);
        }

        if (await localidades.FindAsync(request.RecintoId, request.Id, true, token) is null)
        {
            return Result<Unit>.Failure(LocalidadAccess.NotFound);
        }

        if (await localidades.DeactivateAsync(request.RecintoId, request.Id, token))
        {
            await audit.WriteAsync(actual.UsuarioId, "DELETE", "localidades", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        }

        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
