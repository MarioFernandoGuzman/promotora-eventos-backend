namespace Promotora.Infrastructure.Finance.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class CuentaFinancieraRow
{
    public int Id { get; set; }
    public string Tipo { get; set; } = null!;
    public int? DuenoId { get; set; }
    public string Moneda { get; set; } = null!;
    public decimal Saldo { get; set; }
    public string Estado { get; set; } = null!;
    public DateTime? FechaCierre { get; set; }
}
