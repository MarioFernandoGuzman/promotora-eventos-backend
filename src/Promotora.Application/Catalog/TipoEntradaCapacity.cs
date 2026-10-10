using Promotora.Application.Catalog.Ports;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class TipoEntradaCapacity(ILocalidadRepository localidades, ITipoEntradaRepository tipos)
{
    public async Task<Result<Unit>> ValidateAsync(Evento evento, int? localidadId, int cupo, int? excluirId, CancellationToken ct)
    {
        if (localidadId is not int id) return Result<Unit>.Success(Unit.Value);
        var localidad = await localidades.FindAsync(evento.Datos.RecintoId, id, true, ct);
        if (localidad is null) return Result<Unit>.Failure(CatalogAccess.NotFound);
        if (!localidad.Activo) return Result<Unit>.Failure(CatalogAccess.Inactivo);
        return TipoEntrada.ValidarCapacidad(cupo, await tipos.OtrosCuposAsync(evento.Id, id, excluirId, ct), localidad.Capacidad);
    }
}
