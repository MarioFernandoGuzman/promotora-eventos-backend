namespace Promotora.Infrastructure.Ticketing.Persistence;

// Persistencia Flyway; las reglas se verifican en Domain y PostgreSQL.
public sealed class MesaEventoRow
{
    public long Id { get; set; }
    public long DistribucionId { get; set; }
    public string Etiqueta { get; set; } = null!;
    public string Modalidad { get; set; } = null!;
    public int TipoEntradaId { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
}
