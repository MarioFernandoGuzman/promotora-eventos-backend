using FluentValidation;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed record TipoEntradaDto(int Id, int EventoId, int? LocalidadId, string Nombre, decimal Precio, int CupoTotal, int CupoDisponible, bool Activo)
{
    public static TipoEntradaDto From(TipoEntrada t) => new(t.Id, t.EventoId, t.LocalidadId, t.Datos.Nombre, t.Datos.Precio, t.Datos.CupoTotal, t.CupoDisponible, t.Activo);
}
public sealed record CrearTipoEntradaCommand(int EventoId, int? LocalidadId, DatosTipoEntrada Datos) : ICommand<TipoEntradaDto>;
public sealed record EditarTipoEntradaCommand(int EventoId, int Id, DatosTipoEntrada Datos) : ICommand<TipoEntradaDto>;
public sealed record BajaTipoEntradaCommand(int EventoId, int Id) : ICommand<Unit>;
public sealed record ObtenerTipoEntradaQuery(int EventoId, int Id) : IQuery<TipoEntradaDto>;
public sealed record ListarTiposEntradaQuery(int EventoId, int Page = 1, int PageSize = 20) : IQuery<PaginaDto<TipoEntradaDto>>;

public sealed class DatosTipoEntradaValidator : AbstractValidator<DatosTipoEntrada>
{
    public DatosTipoEntradaValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Precio).InclusiveBetween(0, 99999999.99m).Must(x => decimal.Round(x, 2) == x).WithMessage("El precio admite hasta dos decimales.");
        RuleFor(x => x.CupoTotal).GreaterThanOrEqualTo(0);
    }
}
public sealed class CrearTipoEntradaValidator : AbstractValidator<CrearTipoEntradaCommand>
{
    public CrearTipoEntradaValidator() { RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.LocalidadId).GreaterThan(0).When(x => x.LocalidadId.HasValue); RuleFor(x => x.Datos).NotNull().SetValidator(new DatosTipoEntradaValidator()); }
}
public sealed class EditarTipoEntradaValidator : AbstractValidator<EditarTipoEntradaCommand>
{
    public EditarTipoEntradaValidator() { RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.Id).GreaterThan(0); RuleFor(x => x.Datos).NotNull().SetValidator(new DatosTipoEntradaValidator()); }
}
public sealed class ListarTiposEntradaValidator : AbstractValidator<ListarTiposEntradaQuery>
{
    public ListarTiposEntradaValidator() { RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100); }
}
