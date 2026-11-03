namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class CalificacionPresentadorView
{
    public DateOnly? Fecha { get; set; }
    public int? PresentadorId { get; set; }
    public string? PresentadorNombre { get; set; }
    public int? EventoId { get; set; }
    public int? CategoriaId { get; set; }
    public int? RecintoId { get; set; }
    public int? DuenoId { get; set; }
    public string? EventoEstado { get; set; }
    public long? ResenasAprobadas { get; set; }
    public long? SumaCalificaciones { get; set; }
    public decimal? CalificacionPromedio { get; set; }
}
