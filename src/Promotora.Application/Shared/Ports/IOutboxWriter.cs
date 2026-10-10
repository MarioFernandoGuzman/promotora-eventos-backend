using Promotora.Application.Shared.Messaging;

namespace Promotora.Application.Shared.Ports;

public interface IOutboxWriter
{
    // Participa en la transacción de negocio abierta por IUnitOfWork; nunca publica.
    Task<long> EscribirAsync(NuevoMensajeOutbox mensaje, CancellationToken ct);
}
