namespace Promotora.Infrastructure.Finance.Persistence;

public sealed class PagoSimuladoRow
{
    public Guid Referencia { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public DateTime Fecha { get; set; }
}
