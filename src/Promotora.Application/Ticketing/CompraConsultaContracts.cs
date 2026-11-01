using FluentValidation;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

public sealed record ObtenerCompraQuery(Guid PublicId) : IQuery<CompraCreadaDto>;
public sealed record MisComprasQuery(int Page = 1, int PageSize = 20) : IQuery<PaginaDto<CompraCreadaDto>>;
public sealed record MisBoletosQuery(int Page = 1, int PageSize = 20) : IQuery<PaginaDto<BoletoClienteDto>>;
public sealed record CancelarCompraCommand(Guid PublicId) : ICommand<CompraCreadaDto>;
public sealed record BoletoClienteDto(Guid PublicId, Guid CompraPublicId, int EventoId, int TipoEntradaId,
    string CodigoQr, string Estado, DatosAsistente Asistente, DateTime FechaEmision, DateTime? FechaUso, UbicacionBoletoDto? Ubicacion = null);

public sealed class ObtenerCompraValidator : AbstractValidator<ObtenerCompraQuery>
{
    public ObtenerCompraValidator() => RuleFor(x => x.PublicId).NotEmpty();
}
public sealed class CancelarCompraValidator : AbstractValidator<CancelarCompraCommand>
{
    public CancelarCompraValidator() => RuleFor(x => x.PublicId).NotEmpty();
}
public sealed class MisComprasValidator : AbstractValidator<MisComprasQuery>
{
    public MisComprasValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class MisBoletosValidator : AbstractValidator<MisBoletosQuery>
{
    public MisBoletosValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
