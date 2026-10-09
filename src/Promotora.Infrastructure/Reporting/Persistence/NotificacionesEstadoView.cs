namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class NotificacionesEstadoView
{
    public DateOnly? Fecha { get; set; }
    public string? Tipo { get; set; }
    public string? Estado { get; set; }
    public long? Notificaciones { get; set; }
}
