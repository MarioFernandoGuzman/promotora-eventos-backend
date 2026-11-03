namespace Promotora.Infrastructure.Identity.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class ClienteRow
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
}
