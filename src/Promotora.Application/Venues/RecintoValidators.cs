using FluentValidation;
using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed class DatosRecintoValidator : AbstractValidator<DatosRecinto>
{
    public DatosRecintoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Direccion).MaximumLength(255);
        RuleFor(x => x.Ciudad).MaximumLength(100);
        RuleFor(x => x.ImagenPlanoUrl).MaximumLength(255);
        RuleFor(x => x.CapacidadTotal).GreaterThan(0).When(x => x.CapacidadTotal.HasValue);
        RuleFor(x => x.Latitud).InclusiveBetween(-90m, 90m).When(x => x.Latitud.HasValue);
        RuleFor(x => x.Longitud).InclusiveBetween(-180m, 180m).When(x => x.Longitud.HasValue);
        RuleFor(x => x.Latitud).Must(x => !x.HasValue || decimal.Round(x.Value, 6) == x.Value).WithMessage("Usa como máximo seis decimales.");
        RuleFor(x => x.Longitud).Must(x => !x.HasValue || decimal.Round(x.Value, 6) == x.Value).WithMessage("Usa como máximo seis decimales.");
    }
}

public sealed class CrearRecintoValidator : AbstractValidator<CrearRecintoCommand>
{
    public CrearRecintoValidator()
    {
        RuleFor(x => x.DuenoId).GreaterThan(0).When(x => x.DuenoId.HasValue);
        RuleFor(x => x.Datos).NotNull().SetValidator(new DatosRecintoValidator());
    }
}

public sealed class ActualizarRecintoValidator : AbstractValidator<ActualizarRecintoCommand>
{
    public ActualizarRecintoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull().SetValidator(new DatosRecintoValidator());
    }
}

public sealed class ListarRecintosValidator : AbstractValidator<ListarRecintosQuery>
{
    public ListarRecintosValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
