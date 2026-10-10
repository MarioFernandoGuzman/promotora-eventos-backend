using FluentValidation;
using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Ticketing;

public sealed record ValidarBoletoCommand(string CodigoQr) : ICommand<BoletoValidadoDto>;
public sealed record BoletoValidadoDto(Guid PublicId, string Estado, DateTime FechaUso);
public sealed class ValidarBoletoValidator : AbstractValidator<ValidarBoletoCommand>
{
    public ValidarBoletoValidator() => RuleFor(x => x.CodigoQr).NotEmpty().Length(79);
}
