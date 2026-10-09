namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class InscritosCarrerasView
{
    public DateOnly? Fecha { get; set; }
    public int? EventoId { get; set; }
    public string? EventoNombre { get; set; }
    public string? EventoEstado { get; set; }
    public int? CategoriaId { get; set; }
    public int? RecintoId { get; set; }
    public int? DuenoId { get; set; }
    public string? Distancia { get; set; }
    public long? Inscritos { get; set; }
    public long? ParticipantesValidados { get; set; }
}
