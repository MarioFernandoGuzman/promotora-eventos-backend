namespace Promotora.Infrastructure.Ticketing.Persistence;

// Persistencia Flyway; las reglas se verifican en Domain y PostgreSQL.
public sealed class AsientoEventoRow
{
    public long Id { get; set; }
    public long DistribucionId { get; set; }
    public long? MesaId { get; set; }
    public int TipoEntradaId { get; set; }
    public string Etiqueta { get; set; } = null!;
    public string? Fila { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public string Estado { get; set; } = "DISPONIBLE";
}
