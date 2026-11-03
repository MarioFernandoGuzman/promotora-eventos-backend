namespace Promotora.Infrastructure.Reporting.Persistence;

// Modelo de persistencia del esquema Flyway; no representa reglas del dominio.
public sealed class AuditoriaOperacionesView
{
    public DateOnly? Fecha { get; set; }
    public string? EntidadAfectada { get; set; }
    public string? Accion { get; set; }
    public long? Operaciones { get; set; }
    public long? UsuariosIdentificados { get; set; }
}
