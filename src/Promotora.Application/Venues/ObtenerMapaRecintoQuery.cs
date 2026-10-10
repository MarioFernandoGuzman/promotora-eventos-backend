using FluentValidation;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed record ObtenerMapaRecintoQuery(int Id, int? EventoId = null) : IQuery<MapaRecintoDto>;

public sealed class ObtenerMapaRecintoValidator : AbstractValidator<ObtenerMapaRecintoQuery>
{
    public ObtenerMapaRecintoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EventoId).GreaterThan(0).When(x => x.EventoId.HasValue);
    }
}

public sealed class ObtenerMapaRecintoHandler(IMapaRecintoQueryPort mapas) : IQueryHandler<ObtenerMapaRecintoQuery, MapaRecintoDto>
{
    public async Task<Result<MapaRecintoDto>> HandleAsync(ObtenerMapaRecintoQuery request, CancellationToken ct)
    {
        var mapa = await mapas.ReadAsync(request.Id, request.EventoId, ct);
        return mapa is null ? Result<MapaRecintoDto>.Failure(new Error("venues.mapa_not_found", "No se encontró el mapa público para este recinto y evento.", ErrorType.NotFound))
            : Result<MapaRecintoDto>.Success(mapa);
    }
}
