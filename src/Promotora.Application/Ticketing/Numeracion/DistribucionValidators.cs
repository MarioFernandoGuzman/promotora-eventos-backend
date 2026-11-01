using FluentValidation;

namespace Promotora.Application.Ticketing.Numeracion;

public sealed class GuardarDistribucionValidator : AbstractValidator<GuardarDistribucionCommand>
{
    public GuardarDistribucionValidator()
    {
        RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.LocalidadId).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull();
        When(x => x.Datos is not null, () =>
        {
            RuleFor(x => x.Datos.ImagenPlanoUrl).MaximumLength(255).Must(url => url is null ||
                url.StartsWith("/api/v1/imagenes/", StringComparison.Ordinal) && !url.Contains("..", StringComparison.Ordinal));
            RuleFor(x => x.Datos.Mesas).NotNull().Must(m => m is not null && m.Count <= 500 && m.All(x => x is not null));
            RuleFor(x => x.Datos.Asientos).NotEmpty().Must(a => a is not null && a.Count <= 2000 && a.All(x => x is not null));
            RuleForEach(x => x.Datos.Mesas).NotNull().ChildRules(m =>
            {
                m.RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
                m.RuleFor(x => x.Clave).NotEmpty().MaximumLength(50);
                m.RuleFor(x => x.Etiqueta).NotEmpty().MaximumLength(50);
                m.RuleFor(x => x.Modalidad).Must(x => x is "COMPLETA" or "POR_SILLA");
                m.RuleFor(x => x.TipoEntradaId).GreaterThan(0);
                m.RuleFor(x => x.X).InclusiveBetween(0m, 1m); m.RuleFor(x => x.Y).InclusiveBetween(0m, 1m);
            });
            RuleForEach(x => x.Datos.Asientos).NotNull().ChildRules(a =>
            {
                a.RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
                a.RuleFor(x => x.Etiqueta).NotEmpty().MaximumLength(50);
                a.RuleFor(x => x.MesaClave).MaximumLength(50); a.RuleFor(x => x.Fila).MaximumLength(50);
                a.RuleFor(x => x.TipoEntradaId).GreaterThan(0);
                a.RuleFor(x => x.X).InclusiveBetween(0m, 1m); a.RuleFor(x => x.Y).InclusiveBetween(0m, 1m);
            });
        });
    }
}

public sealed class PublicarDistribucionValidator : AbstractValidator<PublicarDistribucionCommand>
{
    public PublicarDistribucionValidator() { RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.LocalidadId).GreaterThan(0); }
}
public sealed class BloquearAsientosValidator : AbstractValidator<BloquearAsientosCommand>
{
    public BloquearAsientosValidator()
    {
        RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.LocalidadId).GreaterThan(0);
        RuleFor(x => x.AsientoIds).NotEmpty().Must(ids => ids is not null && ids.Count <= 2000 && ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count);
    }
}
public sealed class ObtenerDistribucionValidator : AbstractValidator<ObtenerDistribucionQuery>
{
    public ObtenerDistribucionValidator()
    {
        RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.LocalidadId).GreaterThan(0);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class ListarAsientosValidator : AbstractValidator<ListarAsientosQuery>
{
    public ListarAsientosValidator()
    {
        RuleFor(x => x.EventoId).GreaterThan(0); RuleFor(x => x.LocalidadId).GreaterThan(0);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.MesaId).GreaterThan(0).When(x => x.MesaId.HasValue);
    }
}
