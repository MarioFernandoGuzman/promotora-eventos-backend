namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class OcupacionLocalidadView
{
    public int? EventoId { get; set; }
    public string? EventoNombre { get; set; }
    public string? EventoEstado { get; set; }
    public DateOnly? Fecha { get; set; }
    public int? CategoriaId { get; set; }
    public int? RecintoId { get; set; }
    public int? DuenoId { get; set; }
    public int? LocalidadId { get; set; }
    public string? LocalidadNombre { get; set; }
    public int? Capacidad { get; set; }
    public long? CupoTotal { get; set; }
    public long? CupoDisponible { get; set; }
    public long? CupoOcupado { get; set; }
    public decimal? PorcentajeOcupacion { get; set; }
}
