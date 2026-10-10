using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class ObtenerCategoriaEventoHandler(ICategoriaEventoRepository repo, CatalogAccess access) : IQueryHandler<ObtenerCategoriaEventoQuery, CategoriaEventoDto>
{
    public async Task<Result<CategoriaEventoDto>> HandleAsync(ObtenerCategoriaEventoQuery request, CancellationToken ct)
    {
        if (await access.ActorAsync(false, ct) is null) return Result<CategoriaEventoDto>.Failure(CatalogAccess.Forbidden);
        var entity = await repo.FindAsync(request.Id, false, ct);
        return entity is null ? Result<CategoriaEventoDto>.Failure(CatalogAccess.NotFound) : Result<CategoriaEventoDto>.Success(CategoriaEventoDto.From(entity));
    }
}

public sealed class ListarCategoriaEventoHandler(ICategoriaEventoRepository repo, CatalogAccess access) : IQueryHandler<ListarCategoriaEventoQuery, PaginaDto<CategoriaEventoDto>>
{
    public async Task<Result<PaginaDto<CategoriaEventoDto>>> HandleAsync(ListarCategoriaEventoQuery request, CancellationToken ct)
    {
        if (await access.ActorAsync(false, ct) is null) return Result<PaginaDto<CategoriaEventoDto>>.Failure(CatalogAccess.Forbidden);
        return Result<PaginaDto<CategoriaEventoDto>>.Success(await repo.ListAsync(request.Page, request.PageSize, ct));
    }
}
