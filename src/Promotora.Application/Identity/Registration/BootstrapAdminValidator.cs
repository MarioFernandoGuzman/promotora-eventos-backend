using FluentValidation;

namespace Promotora.Application.Identity.Registration;

public sealed class BootstrapAdminValidator : AbstractValidator<BootstrapAdminCommand>
{
    public BootstrapAdminValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(150).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}
