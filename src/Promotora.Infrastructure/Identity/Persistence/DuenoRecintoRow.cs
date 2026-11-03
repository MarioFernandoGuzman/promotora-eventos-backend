namespace Promotora.Infrastructure.Identity.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class DuenoRecintoRow
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Empresa { get; set; } = null!;
    public string? Nit { get; set; }
}
