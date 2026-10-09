namespace Promotora.Infrastructure.Catalog.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class EventoPresentadorRow
{
    public int EventoId { get; set; }
    public int PresentadorId { get; set; }
    public string? Rol { get; set; }
}
