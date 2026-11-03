using Promotora.Application.Catalog.Ports;
using Promotora.Application.Venues;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class EventoAccess(VenuesAccess venues, IRecintoRepository recintos, IEventoRepository eventos)
{
    public async Task<Result<Evento>> FindAsync(int id, bool bloquear, bool requiereRecintoActivo, CancellationToken ct)
    {
        var actor = await venues.ActorAsync(bloquear, ct);
        if (actor is null) return Result<Evento>.Failure(CatalogAccess.Forbidden);
        // Lectura inicial para fijar orden: usuario → recinto → evento.
        var initial = await eventos.FindAsync(id, false, ct);
        if (initial is null) return Result<Evento>.Failure(CatalogAccess.NotFound);
        var recinto = await recintos.FindAsync(initial.Datos.RecintoId, bloquear, ct);
        if (recinto is null || !VenuesAccess.Permite(actor, recinto.DuenoId)) return Result<Evento>.Failure(CatalogAccess.Forbidden);
        if (requiereRecintoActivo && !recinto.Activo) return Result<Evento>.Failure(CatalogAccess.Inactivo);
        var evento = bloquear ? await eventos.FindAsync(id, true, ct) : initial;
        if (evento is null) return Result<Evento>.Failure(CatalogAccess.NotFound);
        if (evento.Datos.RecintoId != initial.Datos.RecintoId) return Result<Evento>.Failure(new Error("catalog.concurrent", "El recinto cambió concurrentemente.", ErrorType.Conflict));
        return Result<Evento>.Success(evento);
    }
}
