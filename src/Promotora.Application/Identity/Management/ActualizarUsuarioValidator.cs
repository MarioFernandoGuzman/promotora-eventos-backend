using FluentValidation;

namespace Promotora.Application.Identity.Management;

public sealed class ActualizarUsuarioValidator : AbstractValidator<ActualizarUsuarioCommand>
{
    public ActualizarUsuarioValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(150).EmailAddress();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Apellido).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Perfil).NotNull();
        When(x => x.Perfil is not null, () =>
        {
            RuleFor(x => x.Perfil.Telefono).MaximumLength(20);
            RuleFor(x => x.Perfil.Empresa).MaximumLength(150);
            RuleFor(x => x.Perfil.Nit).MaximumLength(20);
            RuleFor(x => x.Perfil.NivelAcceso).Equal("ESTANDAR");
        });
    }
}
