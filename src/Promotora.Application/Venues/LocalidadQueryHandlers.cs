using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed class ObtenerLocalidadHandler(ILocalidadRepository localidades, LocalidadAccess access) : IQueryHandler<ObtenerLocalidadQuery, LocalidadDto>
{
    public async Task<Result<LocalidadDto>> HandleAsync(ObtenerLocalidadQuery request, CancellationToken ct)
    {
        var recinto = await access.RecintoAsync(request.RecintoId, false, false, ct);
        if (recinto.IsFailure)
        {
            return Result<LocalidadDto>.Failure(recinto.Errors);
        }

        var localidad = await localidades.FindAsync(request.RecintoId, request.Id, false, ct);
        return localidad is null ? Result<LocalidadDto>.Failure(LocalidadAccess.NotFound) : Result<LocalidadDto>.Success(LocalidadDto.From(localidad));
    }
}

public sealed class ListarLocalidadesHandler(ILocalidadRepository localidades, LocalidadAccess access) : IQueryHandler<ListarLocalidadesQuery, LocalidadesPaginaDto>
{
    public async Task<Result<LocalidadesPaginaDto>> HandleAsync(ListarLocalidadesQuery request, CancellationToken ct)
    {
        var recinto = await access.RecintoAsync(request.RecintoId, false, false, ct);
        return recinto.IsFailure ? Result<LocalidadesPaginaDto>.Failure(recinto.Errors)
            : Result<LocalidadesPaginaDto>.Success(await localidades.ListAsync(request.RecintoId, request.Page, request.PageSize, ct));
    }
}
