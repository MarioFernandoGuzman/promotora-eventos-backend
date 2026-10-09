using FluentValidation;
using Promotora.Domain.Identity;

namespace Promotora.Application.Identity.Registration;

public sealed class CrearUsuarioValidator : AbstractValidator<CrearUsuarioCommand>
{
    public CrearUsuarioValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(150).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Apellido).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rol).IsInEnum();
        RuleFor(x => x.Perfil).NotNull();
        When(x => x.Perfil is not null, () =>
        {
            RuleFor(x => x.Perfil.Telefono).MaximumLength(20);
            RuleFor(x => x.Perfil.Nit).MaximumLength(20);
            RuleFor(x => x.Perfil.Empresa).MaximumLength(150);
            RuleFor(x => x.Perfil.NivelAcceso).Equal("ESTANDAR");
            When(x => x.Rol == RolUsuario.DUENO_RECINTO, () => RuleFor(x => x.Perfil.Empresa).NotEmpty());
        });
    }
}
