namespace Promotora.Infrastructure.Shared.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class MensajeProcesadoRow
{
    public long MensajeId { get; set; }
    public string Consumidor { get; set; } = null!;
    public DateTime Fecha { get; set; }
}
