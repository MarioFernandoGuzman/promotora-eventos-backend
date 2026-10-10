using FluentValidation;
using Promotora.Domain.Catalog;

namespace Promotora.Application.Catalog;

public sealed class DatosPresentadorValidator : AbstractValidator<DatosPresentador>
{
    public DatosPresentadorValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Tipo).Must(x => x is "ARTISTA" or "EQUIPO" or "PRESENTADOR" or "ORGANIZACION").WithMessage("Tipo de presentador inválido.");
        RuleFor(x => x.ImagenUrl).MaximumLength(255);
    }
}

public sealed class CrearPresentadorValidator : AbstractValidator<CrearPresentadorCommand>
{
    public CrearPresentadorValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new DatosPresentadorValidator());
}

public sealed class ActualizarPresentadorValidator : AbstractValidator<ActualizarPresentadorCommand>
{
    public ActualizarPresentadorValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull().SetValidator(new DatosPresentadorValidator());
    }
}

public sealed class ListarPresentadorValidator : AbstractValidator<ListarPresentadorQuery>
{
    public ListarPresentadorValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
