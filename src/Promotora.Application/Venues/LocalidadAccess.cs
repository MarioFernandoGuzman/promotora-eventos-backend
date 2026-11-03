using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed class LocalidadAccess(VenuesAccess access, IRecintoRepository recintos)
{
    public async Task<Result<Recinto>> RecintoAsync(int recintoId, bool bloquear, bool requiereActivo, CancellationToken ct)
    {
        var actor = await access.ActorAsync(bloquear, ct);
        if (actor is null)
        {
            return Result<Recinto>.Failure(VenuesAccess.Forbidden);
        }

        var recinto = await recintos.FindAsync(recintoId, bloquear, ct);
        if (recinto is null)
        {
            return Result<Recinto>.Failure(VenuesAccess.NotFound);
        }

        if (!VenuesAccess.Permite(actor, recinto.DuenoId))
        {
            return Result<Recinto>.Failure(VenuesAccess.Forbidden);
        }

        if (requiereActivo && !recinto.Activo)
        {
            return Result<Recinto>.Failure(new Error("venues.inactivo", "El recinto está inactivo.", ErrorType.Conflict));
        }

        return Result<Recinto>.Success(recinto);
    }

    public static readonly Error NotFound = new("venues.localidad_not_found", "No se encontró la localidad en este recinto.", ErrorType.NotFound);
}
