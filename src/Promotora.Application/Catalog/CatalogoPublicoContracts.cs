using FluentValidation;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed record EventoResumenPublicoDto(int Id, string Nombre, DateTime FechaInicio, DateTime? FechaFin, string Estado, string? ImagenUrl,
    int CategoriaId, string Categoria, int RecintoId, string Recinto, string? Ciudad);
public sealed record CategoriaPublicaDto(int Id, string Nombre);
public sealed record RecintoPublicoDto(int Id, string Nombre, string? Ciudad, string? Direccion, decimal? Latitud, decimal? Longitud, string? ImagenPlanoUrl);
public sealed record PresentadorPublicoDto(int Id, string Nombre, string Tipo, string? Rol, string? Biografia, string? ImagenUrl);
public sealed record TipoEntradaPublicoDto(int Id, int? LocalidadId, string Nombre, decimal Precio, int CupoTotal, int CupoDisponible);
public sealed record EventoPublicoDto(int Id, string Nombre, string? Descripcion, DateTime FechaInicio, DateTime? FechaFin,
    DateTime? FechaInicioVenta, DateTime? FechaFinVenta, int MaxBoletosPorCompra, string Estado, string? ImagenUrl,
    CategoriaPublicaDto Categoria, RecintoPublicoDto Recinto, IReadOnlyList<PresentadorPublicoDto> Presentadores, IReadOnlyList<TipoEntradaPublicoDto> TiposEntrada);
public sealed record ListarCatalogoPublicoQuery(int? CategoriaId = null, string? Ciudad = null, DateTime? Desde = null, DateTime? Hasta = null,
    string? Texto = null, int Page = 1, int PageSize = 20) : IQuery<PaginaDto<EventoResumenPublicoDto>>;
public sealed record ObtenerEventoPublicoQuery(int Id) : IQuery<EventoPublicoDto>;

public sealed class ListarCatalogoPublicoValidator : AbstractValidator<ListarCatalogoPublicoQuery>
{
    public ListarCatalogoPublicoValidator()
    {
        RuleFor(x => x.CategoriaId).GreaterThan(0).When(x => x.CategoriaId.HasValue);
        RuleFor(x => x.Ciudad).MaximumLength(100);
        RuleFor(x => x.Texto).MaximumLength(200);
        RuleFor(x => x.Desde).Must(x => x is null || x.Value.Kind == DateTimeKind.Utc);
        RuleFor(x => x.Hasta).Must(x => x is null || x.Value.Kind == DateTimeKind.Utc);
        RuleFor(x => x).Must(x => x.Desde is null || x.Hasta is null || x.Desde <= x.Hasta).WithMessage("desde debe ser anterior o igual a hasta.");
        RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListarCatalogoPublicoHandler(ICatalogoPublicoQueryPort port) : IQueryHandler<ListarCatalogoPublicoQuery, PaginaDto<EventoResumenPublicoDto>>
{
    public async Task<Result<PaginaDto<EventoResumenPublicoDto>>> HandleAsync(ListarCatalogoPublicoQuery request, CancellationToken ct) =>
        Result<PaginaDto<EventoResumenPublicoDto>>.Success(await port.ListAsync(request, ct));
}
public sealed class ObtenerEventoPublicoHandler(ICatalogoPublicoQueryPort port) : IQueryHandler<ObtenerEventoPublicoQuery, EventoPublicoDto>
{
    public async Task<Result<EventoPublicoDto>> HandleAsync(ObtenerEventoPublicoQuery request, CancellationToken ct)
    {
        var dto = await port.FindAsync(request.Id, ct);
        return dto is null ? Result<EventoPublicoDto>.Failure(CatalogAccess.NotFound) : Result<EventoPublicoDto>.Success(dto);
    }
}
