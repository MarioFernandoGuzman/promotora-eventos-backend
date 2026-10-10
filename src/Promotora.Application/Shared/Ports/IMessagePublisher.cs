using Promotora.Application.Shared.Messaging;

namespace Promotora.Application.Shared.Ports;

public interface IMessagePublisher
{
    Task PrepararAsync(CancellationToken ct);
    // Retorna únicamente después de la confirmación del broker. No usar en la transacción de negocio.
    Task PublicarAsync(MensajePublicable mensaje, CancellationToken ct);
}
