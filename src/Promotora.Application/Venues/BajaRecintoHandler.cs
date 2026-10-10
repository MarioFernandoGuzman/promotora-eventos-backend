using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed class BajaRecintoHandler(IRecintoRepository recintos, IVenuesTransaction transaction,
    VenuesAccess access, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<BajaRecintoCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(BajaRecintoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var actor = await access.ActorAsync(true, token);
        if (actor is null)
        {
            return Result<Unit>.Failure(VenuesAccess.Forbidden);
        }

        var recinto = await recintos.FindAsync(request.Id, true, token);
        if (recinto is null)
        {
            return Result<Unit>.Failure(VenuesAccess.NotFound);
        }

        if (!VenuesAccess.Permite(actor, recinto.DuenoId))
        {
            return Result<Unit>.Failure(VenuesAccess.Forbidden);
        }

        if (await recintos.DeactivateAsync(recinto.Id, token))
        {
            await audit.WriteAsync(actual.UsuarioId, "DELETE", "recintos", recinto.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        }

        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
