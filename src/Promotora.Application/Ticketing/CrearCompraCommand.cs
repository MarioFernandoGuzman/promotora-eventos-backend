using FluentValidation;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

public sealed record ItemCompra(int TipoEntradaId, int Cantidad, IReadOnlyList<DatosAsistente>? Asistentes = null);
public sealed record CrearCompraCommand(int EventoId, IReadOnlyList<ItemCompra> Items, string IdempotencyKey) : ICommand<CrearCompraResultado>;
public sealed record CrearCompraResultado(CompraCreadaDto Compra, bool Reutilizada, bool ProcesamientoDisponible = false);
public sealed record DetalleCompraDto(int TipoEntradaId, int Cantidad, decimal PrecioUnitario, decimal Subtotal);
public sealed record CompraCreadaDto(Guid PublicId, int EventoId, string Estado, DateTime FechaCompra, DateTime ExpiraEn,
    string Moneda, decimal Subtotal, decimal ComisionPlataforma, decimal Impuestos, decimal Total, IReadOnlyList<DetalleCompraDto> Items);

public sealed class CrearCompraValidator : AbstractValidator<CrearCompraCommand>
{
    public CrearCompraValidator()
    {
        RuleFor(x => x.EventoId).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100).Must(x => !string.IsNullOrWhiteSpace(x));
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).NotNull().ChildRules(item =>
        {
            item.RuleFor(x => x.TipoEntradaId).GreaterThan(0);
            item.RuleFor(x => x.Cantidad).GreaterThan(0);
        });
    }
}

public sealed record CompraOptions(int ReservaMinutos = 15, string Moneda = "GTQ");
