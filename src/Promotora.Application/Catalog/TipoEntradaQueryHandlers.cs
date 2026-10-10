using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class ObtenerTipoEntradaHandler(ITipoEntradaRepository tipos, EventoAccess access) : IQueryHandler<ObtenerTipoEntradaQuery, TipoEntradaDto>
{
    public async Task<Result<TipoEntradaDto>> HandleAsync(ObtenerTipoEntradaQuery request, CancellationToken ct)
    {
        var evento = await access.FindAsync(request.EventoId, false, false, ct);
        if (evento.IsFailure) return Result<TipoEntradaDto>.Failure(evento.Errors);
        var tipo = await tipos.FindAsync(request.EventoId, request.Id, false, ct);
        return tipo is null ? Result<TipoEntradaDto>.Failure(CatalogAccess.NotFound) : Result<TipoEntradaDto>.Success(TipoEntradaDto.From(tipo));
    }
}
public sealed class ListarTiposEntradaHandler(ITipoEntradaRepository tipos, EventoAccess access) : IQueryHandler<ListarTiposEntradaQuery, PaginaDto<TipoEntradaDto>>
{
    public async Task<Result<PaginaDto<TipoEntradaDto>>> HandleAsync(ListarTiposEntradaQuery request, CancellationToken ct)
    {
        var evento = await access.FindAsync(request.EventoId, false, false, ct);
        return evento.IsFailure ? Result<PaginaDto<TipoEntradaDto>>.Failure(evento.Errors)
            : Result<PaginaDto<TipoEntradaDto>>.Success(await tipos.ListAsync(request.EventoId, request.Page, request.PageSize, ct));
    }
}
