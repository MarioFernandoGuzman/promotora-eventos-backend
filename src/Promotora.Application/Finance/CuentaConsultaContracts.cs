using FluentValidation;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Finance;

public sealed record CuentasQuery(int? DuenoId = null, string? Moneda = null, string? Tipo = null, string? Estado = null,
    int Page = 1, int PageSize = 20) : IQuery<PaginaDto<CuentaDto>>;
public sealed record ObtenerCuentaQuery(int Id) : IQuery<CuentaDto>;
public sealed record MovimientosCuentaQuery(int Id, string? Tipo = null, int? CompraId = null, DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null, int Page = 1, int PageSize = 20) : IQuery<PaginaDto<MovimientoCuentaDto>>;
public sealed record MovimientoCuentaDto(long Id, int CuentaId, int? CompraId, string Tipo, decimal Monto, DateTime Fecha);

public sealed class CuentasValidator : AbstractValidator<CuentasQuery>
{
    public CuentasValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.DuenoId).GreaterThan(0).When(x => x.DuenoId.HasValue);
        RuleFor(x => x.Moneda).Matches("\\A[A-Z]{3}\\z").When(x => x.Moneda is not null);
        RuleFor(x => x.Tipo).Must(t => t is "DUENO" or "PLATAFORMA" or "IMPUESTOS").When(x => x.Tipo is not null);
        RuleFor(x => x.Estado).Must(e => e is "ACTIVA" or "CERRADA").When(x => x.Estado is not null);
    }
}
public sealed class ObtenerCuentaValidator : AbstractValidator<ObtenerCuentaQuery>
{
    public ObtenerCuentaValidator() => RuleFor(x => x.Id).GreaterThan(0);
}
public sealed class MovimientosCuentaValidator : AbstractValidator<MovimientosCuentaQuery>
{
    public MovimientosCuentaValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.CompraId).GreaterThan(0).When(x => x.CompraId.HasValue);
        RuleFor(x => x.Tipo).Must(t => t is "VENTA" or "COMISION" or "IMPUESTO" or "LIQUIDACION" or "REEMBOLSO").When(x => x.Tipo is not null);
        RuleFor(x => x.Desde).Must(d => d is null || d.Value.Offset == TimeSpan.Zero).WithMessage("Desde debe indicar UTC (Z o +00:00).");
        RuleFor(x => x.Hasta).Must(d => d is null || d.Value.Offset == TimeSpan.Zero).WithMessage("Hasta debe indicar UTC (Z o +00:00).");
        RuleFor(x => x).Must(x => x.Desde is null || x.Hasta is null || x.Desde < x.Hasta).WithMessage("Desde debe ser anterior a hasta.");
    }
}
