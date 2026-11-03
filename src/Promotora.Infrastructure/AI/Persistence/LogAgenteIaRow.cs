using System.Text.Json;

namespace Promotora.Infrastructure.AI.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class LogAgenteIaRow
{
    public long Id { get; set; }
    public int? UsuarioId { get; set; }
    public string Consulta { get; set; } = null!;
    public string? Respuesta { get; set; }
    public JsonDocument? FuentesCitadas { get; set; }
    public int? TiempoRespuestaMs { get; set; }
    public DateTime FechaHora { get; set; }
    public Guid SessionId { get; set; }
    public string? Modelo { get; set; }
    public int? TokensEntrada { get; set; }
    public int? TokensSalida { get; set; }
    public string Estado { get; set; } = null!;
}
