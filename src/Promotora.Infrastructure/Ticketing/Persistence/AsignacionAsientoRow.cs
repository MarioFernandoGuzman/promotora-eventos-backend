namespace Promotora.Infrastructure.Ticketing.Persistence;

// Persistencia Flyway; las reglas se verifican en Domain y PostgreSQL.
public sealed class AsignacionAsientoRow
{
    public long Id { get; set; }
    public long AsientoId { get; set; }
    public int DetalleCompraId { get; set; }
    public int OrdinalBoleto { get; set; }
    public string Estado { get; set; } = "RESERVADA";
    public long? BoletoId { get; set; }
    public DateTime FechaReserva { get; set; }
    public DateTime? FechaConfirmacion { get; set; }
    public DateTime? FechaLiberacion { get; set; }
}
