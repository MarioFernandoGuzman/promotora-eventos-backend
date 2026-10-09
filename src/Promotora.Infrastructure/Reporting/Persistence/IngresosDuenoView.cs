namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class IngresosDuenoView
{
    public DateOnly? Fecha { get; set; }
    public int? DuenoId { get; set; }
    public int? CuentaId { get; set; }
    public string? Moneda { get; set; }
    public int? EventoId { get; set; }
    public int? CategoriaId { get; set; }
    public int? RecintoId { get; set; }
    public string? EventoEstado { get; set; }
    public string? CompraEstado { get; set; }
    public decimal? Ventas { get; set; }
    public decimal? Reembolsos { get; set; }
    public decimal? Liquidaciones { get; set; }
    public decimal? MovimientoNeto { get; set; }
}
