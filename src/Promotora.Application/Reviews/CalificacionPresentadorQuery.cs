using FluentValidation;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reviews;

public sealed record CalificacionPresentadorQuery(int Id) : IQuery<CalificacionPresentadorDto>;
public sealed record CalificacionPresentadorDto(int PresentadorId, decimal? Promedio, long Cantidad);
public sealed class CalificacionPresentadorValidator : AbstractValidator<CalificacionPresentadorQuery>
{
    public CalificacionPresentadorValidator() => RuleFor(x => x.Id).GreaterThan(0);
}
public sealed class CalificacionPresentadorHandler(IResenaRepository resenas) : IQueryHandler<CalificacionPresentadorQuery, CalificacionPresentadorDto>
{
    public async Task<Result<CalificacionPresentadorDto>> HandleAsync(CalificacionPresentadorQuery query, CancellationToken ct)
    {
        var resultado = await resenas.CalificacionAsync(query.Id, ct);
        return resultado is null ? Result<CalificacionPresentadorDto>.Failure(new Error("presentador.no_encontrado", "Presentador no encontrado.", ErrorType.NotFound))
            : Result<CalificacionPresentadorDto>.Success(resultado);
    }
}
