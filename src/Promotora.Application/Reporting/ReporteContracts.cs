using FluentValidation;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Reporting;

public sealed record ReporteFiltros(DateOnly? Desde = null, DateOnly? Hasta = null, int? EventoId = null,
    int? CategoriaId = null, int? RecintoId = null, int? DuenoId = null, string? Estado = null,
    string? Moneda = null, string? Metodo = null, int Page = 1, int PageSize = 20);
public sealed record ReporteQuery(string Codigo, ReporteFiltros Filtros) : IQuery<ReporteDto>;
public sealed record ColumnaReporteDto(string Campo, string Titulo, string Tipo);
public sealed record SeccionReporteDto(string Codigo, IReadOnlyList<ColumnaReporteDto> Columnas,
    IReadOnlyList<string> Agrupacion, IReadOnlyList<IReadOnlyDictionary<string, object?>> Filas,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Totales, long TotalFilas, int Page, int PageSize);
public sealed record ReporteDto(string Codigo, string Titulo, string Nota, ReporteFiltros Filtros,
    IReadOnlyList<SeccionReporteDto> Secciones);

public interface IReporteRepository
{
    Task<Result<ReporteDto>> ConsultarAsync(ReporteQuery query, CancellationToken ct);
}

public sealed class ReporteValidator : AbstractValidator<ReporteQuery>
{
    public ReporteValidator()
    {
        RuleFor(x => x.Codigo).Matches("^R-(0[1-9]|1[0-5])$");
        RuleFor(x => x.Filtros).NotNull();
        When(x => x.Filtros is not null, () =>
        {
            RuleFor(x => x.Filtros.Page).InclusiveBetween(1, 100000);
            RuleFor(x => x.Filtros.PageSize).InclusiveBetween(1, 100);
            RuleFor(x => x.Filtros.EventoId).GreaterThan(0);
            RuleFor(x => x.Filtros.CategoriaId).GreaterThan(0);
            RuleFor(x => x.Filtros.RecintoId).GreaterThan(0);
            RuleFor(x => x.Filtros.DuenoId).GreaterThan(0);
            RuleFor(x => x.Filtros.Moneda).Matches("^[A-Z]{3}$").When(x => x.Filtros.Moneda is not null);
            RuleFor(x => x.Filtros.Estado).Matches("^[A-Z_]{1,30}$").When(x => x.Filtros.Estado is not null);
            RuleFor(x => x.Filtros.Metodo).Matches("^[A-Z_]{1,30}$").When(x => x.Filtros.Metodo is not null);
            RuleFor(x => x.Filtros).Must(f => f.Desde is null || f.Hasta is null || f.Desde < f.Hasta)
                .WithMessage("desde debe ser anterior a hasta (exclusivo); fechas de calendario UTC.");
        });
    }
}
