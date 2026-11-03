namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class AsistenciaEventoView
{
    public DateOnly? Fecha { get; set; }
    public int? EventoId { get; set; }
    public string? EventoNombre { get; set; }
    public string? EventoEstado { get; set; }
    public int? CategoriaId { get; set; }
    public int? RecintoId { get; set; }
    public int? DuenoId { get; set; }
    public string? Moneda { get; set; }
    public long? BoletosEmitidos { get; set; }
    public long? BoletosVendidos { get; set; }
    public long? BoletosUsados { get; set; }
    public long? BoletosAnulados { get; set; }
}
