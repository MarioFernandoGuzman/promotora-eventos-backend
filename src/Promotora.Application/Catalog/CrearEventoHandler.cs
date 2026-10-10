using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Venues;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class CrearEventoHandler(IEventoRepository eventos, IRecintoRepository recintos, ICategoriaEventoRepository categorias,
    ICatalogTransaction transaction, VenuesAccess access, IUsuarioActual actual, IAuditLogger audit, IClock clock) : ICommandHandler<CrearEventoCommand, EventoDto>
{
    public Task<Result<EventoDto>> HandleAsync(CrearEventoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var actor = await access.ActorAsync(true, token);
        if (actor is null) return Result<EventoDto>.Failure(CatalogAccess.Forbidden);
        var recinto = await recintos.FindAsync(request.Datos.RecintoId, true, token);
        if (recinto is null) return Result<EventoDto>.Failure(CatalogAccess.NotFound);
        if (!VenuesAccess.Permite(actor, recinto.DuenoId)) return Result<EventoDto>.Failure(CatalogAccess.Forbidden);
        if (!recinto.Activo) return Result<EventoDto>.Failure(CatalogAccess.Inactivo);
        var categoria = await categorias.FindAsync(request.Datos.CategoriaId, true, token);
        if (categoria is null) return Result<EventoDto>.Failure(CatalogAccess.NotFound);
        if (!categoria.Activo) return Result<EventoDto>.Failure(CatalogAccess.Inactivo);
        var evento = await eventos.InsertAsync(new Evento(0, actual.UsuarioId!.Value, Normalizar(request.Datos), EstadoEvento.BORRADOR, clock.UtcNow), token);
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "eventos", evento.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<EventoDto>.Success(EventoDto.From(evento));
    }, ct);

    internal static DatosEvento Normalizar(DatosEvento datos) => datos with { Nombre = datos.Nombre.Trim(), Descripcion = datos.Descripcion?.Trim(), ImagenUrl = datos.ImagenUrl?.Trim() };
}
