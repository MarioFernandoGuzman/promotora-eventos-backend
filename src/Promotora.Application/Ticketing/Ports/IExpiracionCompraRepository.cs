namespace Promotora.Application.Ticketing.Ports;

public interface IExpiracionCompraRepository
{
    Task<IReadOnlyList<int>> VencidasAsync(DateTime ahora, int lote, CancellationToken ct);
}
