namespace Promotora.Infrastructure.Catalog.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class PresentadorRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Tipo { get; set; } = null!;
    public string? Biografia { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Activo { get; set; }
}
