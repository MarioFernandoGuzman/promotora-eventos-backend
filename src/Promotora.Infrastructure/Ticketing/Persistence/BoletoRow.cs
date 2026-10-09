using System.Text.Json;

namespace Promotora.Infrastructure.Ticketing.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class BoletoRow
{
    public long Id { get; set; }
    public Guid PublicId { get; set; }
    public int DetalleCompraId { get; set; }
    public string CodigoQr { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public string? NombreAsistente { get; set; }
    public string? DocumentoIdentidad { get; set; }
    public JsonDocument? DatosExtra { get; set; }
    public DateTime FechaEmision { get; set; }
    public DateTime? FechaUso { get; set; }
    public int? ValidadoPor { get; set; }
}
