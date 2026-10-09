using FluentValidation;

namespace Promotora.Application.Identity.Sessions;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken).NotEmpty().Length(64).Matches("^[A-Fa-f0-9]{64}$");
}
