using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class ObtenerEventoHandler(EventoAccess access) : IQueryHandler<ObtenerEventoQuery, EventoDto>
{
    public async Task<Result<EventoDto>> HandleAsync(ObtenerEventoQuery request, CancellationToken ct)
    {
        var result = await access.FindAsync(request.Id, false, false, ct);
        return result.IsFailure ? Result<EventoDto>.Failure(result.Errors) : Result<EventoDto>.Success(EventoDto.From(result.Value));
    }
}

public sealed class ListarEventosHandler(IEventoRepository eventos, VenuesAccess access) : IQueryHandler<ListarEventosQuery, PaginaDto<EventoDto>>
{
    public async Task<Result<PaginaDto<EventoDto>>> HandleAsync(ListarEventosQuery request, CancellationToken ct)
    {
        var actor = await access.ActorAsync(false, ct);
        return actor is null ? Result<PaginaDto<EventoDto>>.Failure(CatalogAccess.Forbidden)
            : Result<PaginaDto<EventoDto>>.Success(await eventos.ListAsync(actor.Value.DuenoId, request.Page, request.PageSize, ct));
    }
}
