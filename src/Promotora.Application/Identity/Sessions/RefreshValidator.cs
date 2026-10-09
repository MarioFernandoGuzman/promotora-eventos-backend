using FluentValidation;

namespace Promotora.Application.Identity.Sessions;

public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator() => RuleFor(x => x.RefreshToken).NotEmpty().Length(64).Matches("^[A-Fa-f0-9]{64}$");
}
