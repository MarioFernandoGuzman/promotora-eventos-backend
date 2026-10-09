namespace Promotora.Infrastructure.Identity.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class AdministradorRow
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string NivelAcceso { get; set; } = null!;
}
