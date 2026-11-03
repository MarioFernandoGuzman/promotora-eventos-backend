using System.Text.Json;

namespace Promotora.Infrastructure.Shared.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class OutboxMensajeRow
{
    public long Id { get; set; }
    public string Agregado { get; set; } = null!;
    public string AgregadoId { get; set; } = null!;
    public string Tipo { get; set; } = null!;
    public JsonDocument Payload { get; set; } = null!;
    public bool Publicado { get; set; }
    public DateTime Fecha { get; set; }
    public DateTime? FechaPublicado { get; set; }
    public int Intentos { get; set; }
}
