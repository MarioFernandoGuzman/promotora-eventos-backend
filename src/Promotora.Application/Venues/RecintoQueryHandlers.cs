using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed class ObtenerRecintoHandler(IRecintoRepository recintos, VenuesAccess access) : IQueryHandler<ObtenerRecintoQuery, RecintoDto>
{
    public async Task<Result<RecintoDto>> HandleAsync(ObtenerRecintoQuery request, CancellationToken ct)
    {
        var actor = await access.ActorAsync(false, ct);
        if (actor is null)
        {
            return Result<RecintoDto>.Failure(VenuesAccess.Forbidden);
        }

        var recinto = await recintos.FindAsync(request.Id, false, ct);
        if (recinto is null)
        {
            return Result<RecintoDto>.Failure(VenuesAccess.NotFound);
        }

        return VenuesAccess.Permite(actor, recinto.DuenoId) ? Result<RecintoDto>.Success(RecintoDto.From(recinto))
            : Result<RecintoDto>.Failure(VenuesAccess.Forbidden);
    }
}

public sealed class ListarRecintosHandler(IRecintoRepository recintos, VenuesAccess access) : IQueryHandler<ListarRecintosQuery, RecintosPaginaDto>
{
    public async Task<Result<RecintosPaginaDto>> HandleAsync(ListarRecintosQuery request, CancellationToken ct)
    {
        var actor = await access.ActorAsync(false, ct);
        if (actor is null)
        {
            return Result<RecintosPaginaDto>.Failure(VenuesAccess.Forbidden);
        }

        return Result<RecintosPaginaDto>.Success(await recintos.ListAsync(actor.Value.DuenoId, request.Page, request.PageSize, ct));
    }
}
