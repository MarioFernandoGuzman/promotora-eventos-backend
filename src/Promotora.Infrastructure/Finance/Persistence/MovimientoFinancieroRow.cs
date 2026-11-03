namespace Promotora.Infrastructure.Finance.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class MovimientoFinancieroRow
{
    public long Id { get; set; }
    public int CuentaId { get; set; }
    public int? CompraId { get; set; }
    public string Tipo { get; set; } = null!;
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
    public DateTime Fecha { get; set; }
}
