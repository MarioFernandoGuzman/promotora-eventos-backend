using FluentValidation;
using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed class DatosLocalidadValidator : AbstractValidator<DatosLocalidadDto>
{
    public DatosLocalidadValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Capacidad).GreaterThan(0);
        RuleFor(x => x.Color).Must(Localidad.ColorValido).WithMessage("El color debe ser #RRGGBB.");
        RuleFor(x => x.Geometria).Must(x => x is null || x.EsValido()).WithMessage("La geometría debe ser polygon con al menos tres puntos distintos [x,y] finitos.");
    }
}

public sealed class CrearLocalidadValidator : AbstractValidator<CrearLocalidadCommand>
{
    public CrearLocalidadValidator()
    {
        RuleFor(x => x.RecintoId).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull().SetValidator(new DatosLocalidadValidator());
    }
}

public sealed class ActualizarLocalidadValidator : AbstractValidator<ActualizarLocalidadCommand>
{
    public ActualizarLocalidadValidator()
    {
        RuleFor(x => x.RecintoId).GreaterThan(0);
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull().SetValidator(new DatosLocalidadValidator());
    }
}

public sealed class ListarLocalidadesValidator : AbstractValidator<ListarLocalidadesQuery>
{
    public ListarLocalidadesValidator()
    {
        RuleFor(x => x.RecintoId).GreaterThan(0);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
