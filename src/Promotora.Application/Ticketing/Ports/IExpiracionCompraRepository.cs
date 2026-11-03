namespace Promotora.Application.Ticketing.Ports;

public interface IExpiracionCompraRepository
{
    // No espera: el bloqueo pertenece a la transacción que incluye estado, cupo y auditoría.
    Task<bool> IntentarCoordinarAsync(CancellationToken ct);
    Task<IReadOnlyList<int>> VencidasAsync(DateTime ahora, int lote, CancellationToken ct);
}
