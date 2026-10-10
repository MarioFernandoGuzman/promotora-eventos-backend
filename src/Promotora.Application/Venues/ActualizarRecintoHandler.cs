using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed class ActualizarRecintoHandler(IRecintoRepository recintos, IVenuesTransaction transaction,
    VenuesAccess access, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<ActualizarRecintoCommand, RecintoDto>
{
    public Task<Result<RecintoDto>> HandleAsync(ActualizarRecintoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var actor = await access.ActorAsync(true, token);
        if (actor is null)
        {
            return Result<RecintoDto>.Failure(VenuesAccess.Forbidden);
        }

        var recinto = await recintos.FindAsync(request.Id, true, token);
        if (recinto is null)
        {
            return Result<RecintoDto>.Failure(VenuesAccess.NotFound);
        }

        if (!VenuesAccess.Permite(actor, recinto.DuenoId))
        {
            return Result<RecintoDto>.Failure(VenuesAccess.Forbidden);
        }

        var capacidad = await recintos.CapacidadAsync(recinto.Id, token);
        var edit = recinto.Editar(CrearRecintoHandler.Normalizar(request.Datos), capacidad.Total, capacidad.TieneLocalidades);
        if (edit.IsFailure)
        {
            return Result<RecintoDto>.Failure(edit.Errors);
        }

        await recintos.UpdateAsync(recinto, token);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "recintos", recinto.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<RecintoDto>.Success(RecintoDto.From(recinto));
    }, ct);
}
