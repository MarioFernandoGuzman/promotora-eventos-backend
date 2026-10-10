using FluentValidation;
using Promotora.Domain.Catalog;

namespace Promotora.Application.Catalog;

public sealed class DatosEventoValidator : AbstractValidator<DatosEvento>
{
    public DatosEventoValidator()
    {
        RuleFor(x => x.RecintoId).GreaterThan(0);
        RuleFor(x => x.CategoriaId).GreaterThan(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ImagenUrl).MaximumLength(255);
        RuleFor(x => x.MaxBoletosPorCompra).GreaterThan(0);
        RuleFor(x => x.FechaInicio).Must(x => x.Kind == DateTimeKind.Utc).WithMessage("La fecha debe incluir UTC (Z).");
        RuleFor(x => x.FechaFin).Must(x => x is null || x.Value.Kind == DateTimeKind.Utc);
        RuleFor(x => x.FechaInicioVenta).Must(x => x is null || x.Value.Kind == DateTimeKind.Utc);
        RuleFor(x => x.FechaFinVenta).Must(x => x is null || x.Value.Kind == DateTimeKind.Utc);
        RuleFor(x => x).Must(x => x.FechaFin is null || x.FechaFin >= x.FechaInicio).WithMessage("fechaFin debe ser posterior o igual a fechaInicio.");
        RuleFor(x => x).Must(x => x.FechaInicioVenta is null || x.FechaFinVenta is null || x.FechaFinVenta >= x.FechaInicioVenta).WithMessage("Ventana de venta inválida.");
    }
}

public sealed class CrearEventoValidator : AbstractValidator<CrearEventoCommand>
{
    public CrearEventoValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new DatosEventoValidator());
}

public sealed class ActualizarEventoValidator : AbstractValidator<ActualizarEventoCommand>
{
    public ActualizarEventoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull().SetValidator(new DatosEventoValidator());
    }
}

public sealed class CambiarEstadoEventoValidator : AbstractValidator<CambiarEstadoEventoCommand>
{
    public CambiarEstadoEventoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.EstadoEsperado).Must(x => Enum.GetNames<EstadoEvento>().Contains(x)).WithMessage("Estado esperado inválido.");
        RuleFor(x => x.EstadoNuevo).Must(x => Enum.GetNames<EstadoEvento>().Contains(x)).WithMessage("Estado nuevo inválido.");
    }
}

public sealed class ListarEventosValidator : AbstractValidator<ListarEventosQuery>
{
    public ListarEventosValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
