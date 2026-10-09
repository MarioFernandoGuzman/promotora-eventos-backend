namespace Promotora.Infrastructure.Shared.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class NotificacionRow
{
    public long Id { get; set; }
    public int? UsuarioId { get; set; }
    public string Tipo { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public string? Mensaje { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
}
