using Promotora.Domain.Shared;

namespace Promotora.Domain.Ticketing;

// Entrada confiable del catálogo; Application obtiene precio/evento desde sus puertos.
public sealed record EntradaCompra(int TipoEntradaId, int EventoId, int Cantidad, decimal PrecioUnitario);

public sealed record DetalleCompra
{
    public int TipoEntradaId { get; }
    public int Cantidad { get; }
    public decimal PrecioUnitario { get; }
    public decimal Subtotal { get; }
    private DetalleCompra(int tipoEntradaId, int cantidad, decimal precioUnitario, decimal subtotal)
        => (TipoEntradaId, Cantidad, PrecioUnitario, Subtotal) = (tipoEntradaId, cantidad, precioUnitario, subtotal);

    public static Result<DetalleCompra> Crear(int tipoEntradaId, int cantidad, decimal precioUnitario)
    {
        if (tipoEntradaId <= 0 || cantidad <= 0 || !ImportesCompra.MontoValido(precioUnitario)) return Result<DetalleCompra>.Failure(Invalido);
        decimal subtotal = cantidad * precioUnitario; // INT × NUMERIC(10,2) cabe en decimal.
        return !ImportesCompra.MontoValido(subtotal) ? Result<DetalleCompra>.Failure(Invalido)
            : Result<DetalleCompra>.Success(new(tipoEntradaId, cantidad, precioUnitario, subtotal));
    }
    private static readonly Error Invalido = new("ticketing.detalle", "Tipo, cantidad o precio de entrada inválidos.", ErrorType.Validation);
}
