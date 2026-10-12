using FluentValidation;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reviews;

public sealed record ResenasEventoQuery(int EventoId, int Page = 1, int PageSize = 20) : IQuery<PaginaDto<ResenaDto>>;
public sealed class ResenasEventoValidator : AbstractValidator<ResenasEventoQuery>
{
    public ResenasEventoValidator()
    {
        RuleFor(x => x.EventoId).GreaterThan(0);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class ResenasEventoHandler(IResenaRepository resenas) : IQueryHandler<ResenasEventoQuery, PaginaDto<ResenaDto>>
{
    public async Task<Result<PaginaDto<ResenaDto>>> HandleAsync(ResenasEventoQuery query, CancellationToken ct)
    {
        if (!await resenas.EventoExisteAsync(query.EventoId, ct))
            return Result<PaginaDto<ResenaDto>>.Failure(new Error("evento.no_encontrado", "Evento no encontrado.", ErrorType.NotFound));
        return Result<PaginaDto<ResenaDto>>.Success(await resenas.AdministracionAsync(new("APROBADA", query.EventoId, query.Page, query.PageSize), ct));
    }
}
