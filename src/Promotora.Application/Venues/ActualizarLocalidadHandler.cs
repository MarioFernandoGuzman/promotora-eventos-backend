using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed class ActualizarLocalidadHandler(ILocalidadRepository localidades, IVenuesTransaction transaction,
    LocalidadAccess access, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<ActualizarLocalidadCommand, LocalidadDto>
{
    public Task<Result<LocalidadDto>> HandleAsync(ActualizarLocalidadCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var recinto = await access.RecintoAsync(request.RecintoId, true, true, token);
        if (recinto.IsFailure)
        {
            return Result<LocalidadDto>.Failure(recinto.Errors);
        }

        var localidad = await localidades.FindAsync(request.RecintoId, request.Id, true, token);
        if (localidad is null)
        {
            return Result<LocalidadDto>.Failure(LocalidadAccess.NotFound);
        }

        var datos = request.Datos;
        var edit = localidad.Editar(datos.Nombre.Trim(), datos.Capacidad, datos.Geometria?.ToDomain(), datos.Color?.ToUpperInvariant(),
            recinto.Value.Datos.CapacidadTotal, await localidades.CapacidadOtrasAsync(request.RecintoId, request.Id, token),
            await localidades.MaxCupoEventoAsync(request.Id, token));
        if (edit.IsFailure)
        {
            return Result<LocalidadDto>.Failure(edit.Errors);
        }

        await localidades.UpdateAsync(localidad, token);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "localidades", localidad.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<LocalidadDto>.Success(LocalidadDto.From(localidad));
    }, ct);
}
