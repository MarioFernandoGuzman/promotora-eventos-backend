using Promotora.Domain.Shared;

namespace Promotora.Application.Ticketing.Ports;

public interface IReservaAsientosPort
{
    Task<Result<Unit>> ReservarAsync(int eventoId, IReadOnlyList<ItemCompra> items, CancellationToken ct);
    Task AsignarAsync(int compraId, IReadOnlyList<ItemCompra> items, DateTime ahora, CancellationToken ct);
}
