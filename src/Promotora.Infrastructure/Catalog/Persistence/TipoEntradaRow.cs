namespace Promotora.Infrastructure.Catalog.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class TipoEntradaRow
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public int? LocalidadId { get; set; }
    public string Nombre { get; set; } = null!;
    public decimal Precio { get; set; }
    public int CupoTotal { get; set; }
    public int CupoDisponible { get; set; }
    public bool Activo { get; set; } = true;
}
