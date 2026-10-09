namespace Promotora.Infrastructure.Ticketing.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class CompraRow
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public int ClienteId { get; set; }
    public int EventoId { get; set; }
    public DateTime FechaCompra { get; set; }
    public string Estado { get; set; } = null!;
    public DateTime ExpiraEn { get; set; }
    public DateTime? ProcesandoDesde { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string Moneda { get; set; } = null!;
    public decimal Subtotal { get; set; }
    public decimal Impuestos { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public decimal Total { get; set; }
}
