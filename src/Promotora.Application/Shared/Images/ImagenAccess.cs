using Promotora.Application.Catalog;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Venues;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Shared.Images;

public sealed class ImagenAccess(VenuesAccess venues, IRecintoRepository recintos, EventoConfigurationAccess eventos,
    CatalogAccess catalog, IPresentadorRepository presentadores)
{
    public async Task<Result<string>> AuthorizeAsync(DestinoImagen destino, int id, CancellationToken ct)
    {
        if (destino == DestinoImagen.Evento)
        {
            var evento = await eventos.FindAsync(id, ct);
            return evento.IsFailure ? Result<string>.Failure(evento.Errors) : Result<string>.Success(evento.Value.Estado.ToString());
        }
        if (destino == DestinoImagen.Presentador)
        {
            if (!await catalog.AdminAsync(ct)) return Result<string>.Failure(CatalogAccess.Forbidden);
            var presentador = await presentadores.FindAsync(id, true, ct);
            if (presentador is null) return Result<string>.Failure(CatalogAccess.NotFound);
            return !presentador.Activo ? Result<string>.Failure(CatalogAccess.Inactivo) : Result<string>.Success(string.Empty);
        }
        if (destino != DestinoImagen.Recinto) return Result<string>.Failure(ImagenLimites.Invalida);
        var actor = await venues.ActorAsync(true, ct);
        if (actor is null) return Result<string>.Failure(VenuesAccess.Forbidden);
        var recinto = await recintos.FindAsync(id, true, ct);
        if (recinto is null) return Result<string>.Failure(VenuesAccess.NotFound);
        if (!VenuesAccess.Permite(actor, recinto.DuenoId)) return Result<string>.Failure(VenuesAccess.Forbidden);
        return !recinto.Activo ? Result<string>.Failure(CatalogAccess.Inactivo) : Result<string>.Success(string.Empty);
    }
}
