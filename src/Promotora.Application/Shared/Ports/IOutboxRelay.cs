using Promotora.Application.Shared.Messaging;

namespace Promotora.Application.Shared.Ports;

public interface IOutboxRelay
{
    Task<ResultadoRelayOutbox> PublicarLoteAsync(int tamanoLote, CancellationToken ct);
}
