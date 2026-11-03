namespace Promotora.Infrastructure.Catalog.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class CategoriaEventoRow
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
