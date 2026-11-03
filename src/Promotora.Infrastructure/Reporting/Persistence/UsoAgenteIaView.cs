namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class UsoAgenteIaView
{
    public DateOnly? Fecha { get; set; }
    public string? Modelo { get; set; }
    public string? Estado { get; set; }
    public long? Consultas { get; set; }
    public decimal? TiempoPromedioMs { get; set; }
    public long? TokensEntrada { get; set; }
    public long? TokensSalida { get; set; }
}
