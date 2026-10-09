namespace Promotora.Infrastructure.Identity.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class RefreshTokenRow
{
    public long Id { get; set; }
    public int UsuarioId { get; set; }
    public string TokenHash { get; set; } = null!;
    public Guid FamiliaId { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
    public long? ReemplazadoPorId { get; set; }
    public string? IpOrigen { get; set; }
    public DateTime FechaCreacion { get; set; }
}
