namespace Promotora.Infrastructure.Catalog.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class EventoRow
{
    public int Id { get; set; }
    public int RecintoId { get; set; }
    public int CategoriaId { get; set; }
    public int CreadoPor { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public DateTime? FechaInicioVenta { get; set; }
    public DateTime? FechaFinVenta { get; set; }
    public int MaxBoletosPorCompra { get; set; } = 10;
    public string Estado { get; set; } = null!;
    public string? ImagenUrl { get; set; }
    public DateTime FechaCreacion { get; set; }
}
