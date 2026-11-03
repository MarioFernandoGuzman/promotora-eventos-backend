namespace Promotora.Infrastructure.Reviews.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class ResenaRow
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public int ClienteId { get; set; }
    public short Calificacion { get; set; }
    public string? Comentario { get; set; }
    public string Estado { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
}
