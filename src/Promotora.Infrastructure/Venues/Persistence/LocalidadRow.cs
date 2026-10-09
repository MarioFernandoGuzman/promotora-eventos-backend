using System.Text.Json;

namespace Promotora.Infrastructure.Venues.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class LocalidadRow
{
    public int Id { get; set; }
    public int RecintoId { get; set; }
    public string Nombre { get; set; } = null!;
    public int Capacidad { get; set; }
    public JsonDocument? Geometria { get; set; }
    public string? Color { get; set; }
    public bool Activo { get; set; } = true;
}
