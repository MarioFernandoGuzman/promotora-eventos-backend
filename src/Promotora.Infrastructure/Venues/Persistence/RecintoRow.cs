namespace Promotora.Infrastructure.Venues.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class RecintoRow
{
    public int Id { get; set; }
    public int DuenoId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public string? ImagenPlanoUrl { get; set; }
    public int? CapacidadTotal { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
}
