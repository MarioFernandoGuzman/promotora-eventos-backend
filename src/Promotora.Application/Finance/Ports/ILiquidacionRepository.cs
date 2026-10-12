namespace Promotora.Application.Finance.Ports;

public interface ILiquidacionRepository
{
    // Todas las operaciones requieren transacción y bloqueo previo de la cuenta.
    Task<LiquidacionDto?> FindAsync(int cuentaId, string referencia, CancellationToken ct);
    Task<decimal> SumaMovimientosAsync(int cuentaId, CancellationToken ct);
    Task<LiquidacionDto?> RegistrarAsync(int cuentaId, decimal monto, string referencia, DateTime fecha, CancellationToken ct);
}
