using FluentValidation;
using Promotora.Domain.Catalog;

namespace Promotora.Application.Catalog;

public sealed class DatosCategoriaEventoValidator : AbstractValidator<DatosCategoriaEvento>
{
    public DatosCategoriaEventoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Descripcion).MaximumLength(255);
    }
}

public sealed class CrearCategoriaEventoValidator : AbstractValidator<CrearCategoriaEventoCommand>
{
    public CrearCategoriaEventoValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new DatosCategoriaEventoValidator());
}

public sealed class ActualizarCategoriaEventoValidator : AbstractValidator<ActualizarCategoriaEventoCommand>
{
    public ActualizarCategoriaEventoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull().SetValidator(new DatosCategoriaEventoValidator());
    }
}

public sealed class ListarCategoriaEventoValidator : AbstractValidator<ListarCategoriaEventoQuery>
{
    public ListarCategoriaEventoValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
