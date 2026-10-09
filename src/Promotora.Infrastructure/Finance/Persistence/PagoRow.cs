using System.Text.Json;

namespace Promotora.Infrastructure.Finance.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class PagoRow
{
    public long Id { get; set; }
    public int CompraId { get; set; }
    public string Pasarela { get; set; } = null!;
    public string? Metodo { get; set; }
    public string? Referencia { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = null!;
    public JsonDocument? Respuesta { get; set; }
    public DateTime Fecha { get; set; }
}
