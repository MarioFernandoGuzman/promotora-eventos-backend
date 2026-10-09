using FluentValidation;

namespace Promotora.Application.Identity.Management;

public sealed class CambiarPasswordValidator : AbstractValidator<CambiarPasswordCommand>
{
    public CambiarPasswordValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.PasswordActual).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PasswordNuevo).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}
