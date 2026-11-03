using System.Text.Json;

namespace Promotora.Infrastructure.Audit.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class LogAuditoriaRow
{
    public long Id { get; set; }
    public int? UsuarioId { get; set; }
    public string EntidadAfectada { get; set; } = null!;
    public string? EntidadId { get; set; }
    public string Accion { get; set; } = null!;
    public JsonDocument? Detalle { get; set; }
    public string? IpOrigen { get; set; }
    public DateTime FechaHora { get; set; }
}
