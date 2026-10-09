namespace Promotora.Infrastructure.Ticketing.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class DetalleCompraRow
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public int TipoEntradaId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
