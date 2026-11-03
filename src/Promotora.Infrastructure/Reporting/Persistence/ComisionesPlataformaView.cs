namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class ComisionesPlataformaView
{
    public DateOnly? Fecha { get; set; }
    public string? Moneda { get; set; }
    public int? EventoId { get; set; }
    public int? CategoriaId { get; set; }
    public int? RecintoId { get; set; }
    public int? DuenoId { get; set; }
    public string? EventoEstado { get; set; }
    public string? CompraEstado { get; set; }
    public decimal? Comisiones { get; set; }
    public decimal? Reembolsos { get; set; }
    public decimal? ComisionNeta { get; set; }
}
