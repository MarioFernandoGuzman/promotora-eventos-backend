namespace Promotora.Infrastructure.Ticketing.Persistence;

// Persistencia Flyway; las reglas se verifican en Domain y PostgreSQL.
public sealed class DistribucionEventoRow
{
    public long Id { get; set; }
    public int EventoId { get; set; }
    public int LocalidadId { get; set; }
    public string Estado { get; set; } = "BORRADOR";
    public string? ImagenPlanoUrl { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaPublicacion { get; set; }
}
