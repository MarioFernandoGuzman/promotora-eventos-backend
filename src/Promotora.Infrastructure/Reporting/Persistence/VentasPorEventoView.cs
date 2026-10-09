namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class VentasPorEventoView
{
    public DateOnly? Fecha { get; set; }
    public int? EventoId { get; set; }
    public string? EventoNombre { get; set; }
    public string? EventoEstado { get; set; }
    public int? CategoriaId { get; set; }
    public int? RecintoId { get; set; }
    public int? DuenoId { get; set; }
    public string? CompraEstado { get; set; }
    public string? Moneda { get; set; }
    public long? ComprasCompletadas { get; set; }
    public decimal? BoletosVendidos { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Impuestos { get; set; }
    public decimal? ComisionPlataforma { get; set; }
    public decimal? Total { get; set; }
}
