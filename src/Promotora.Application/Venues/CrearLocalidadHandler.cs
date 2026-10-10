using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed class CrearLocalidadHandler(ILocalidadRepository localidades, IVenuesTransaction transaction,
    LocalidadAccess access, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<CrearLocalidadCommand, LocalidadDto>
{
    public Task<Result<LocalidadDto>> HandleAsync(CrearLocalidadCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var recinto = await access.RecintoAsync(request.RecintoId, true, true, token);
        if (recinto.IsFailure)
        {
            return Result<LocalidadDto>.Failure(recinto.Errors);
        }

        var datos = request.Datos;
        long otras = await localidades.CapacidadOtrasAsync(request.RecintoId, null, token);
        var capacidad = Localidad.ValidarCapacidad(datos.Capacidad, recinto.Value.Datos.CapacidadTotal, otras, 0);
        if (capacidad.IsFailure)
        {
            return Result<LocalidadDto>.Failure(capacidad.Errors);
        }

        var localidad = await localidades.InsertAsync(new Localidad(0, request.RecintoId, datos.Nombre.Trim(), datos.Capacidad,
            datos.Geometria?.ToDomain(), datos.Color?.ToUpperInvariant(), true), token);
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "localidades", localidad.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<LocalidadDto>.Success(LocalidadDto.From(localidad));
    }, ct);
}
