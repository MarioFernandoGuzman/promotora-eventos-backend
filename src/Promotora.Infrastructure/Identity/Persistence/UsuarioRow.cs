namespace Promotora.Infrastructure.Identity.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class UsuarioRow
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool DebeCambiarPassword { get; set; }
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string Rol { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public DateTime FechaRegistro { get; set; }
}
