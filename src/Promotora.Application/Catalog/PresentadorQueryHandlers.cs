using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class ObtenerPresentadorHandler(IPresentadorRepository repo, CatalogAccess access) : IQueryHandler<ObtenerPresentadorQuery, PresentadorDto>
{
    public async Task<Result<PresentadorDto>> HandleAsync(ObtenerPresentadorQuery request, CancellationToken ct)
    {
        if (await access.ActorAsync(false, ct) is null) return Result<PresentadorDto>.Failure(CatalogAccess.Forbidden);
        var entity = await repo.FindAsync(request.Id, false, ct);
        return entity is null ? Result<PresentadorDto>.Failure(CatalogAccess.NotFound) : Result<PresentadorDto>.Success(PresentadorDto.From(entity));
    }
}

public sealed class ListarPresentadorHandler(IPresentadorRepository repo, CatalogAccess access) : IQueryHandler<ListarPresentadorQuery, PaginaDto<PresentadorDto>>
{
    public async Task<Result<PaginaDto<PresentadorDto>>> HandleAsync(ListarPresentadorQuery request, CancellationToken ct)
    {
        if (await access.ActorAsync(false, ct) is null) return Result<PaginaDto<PresentadorDto>>.Failure(CatalogAccess.Forbidden);
        return Result<PaginaDto<PresentadorDto>>.Success(await repo.ListAsync(request.Page, request.PageSize, ct));
    }
}
