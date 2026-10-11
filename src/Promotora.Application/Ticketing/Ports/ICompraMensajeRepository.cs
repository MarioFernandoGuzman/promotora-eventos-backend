using Promotora.Domain.Shared;

namespace Promotora.Application.Ticketing.Ports;

public interface ICompraMensajeRepository
{
    // Verifica la identidad del outbox y recupera asistentes locales; requiere transacción.
    Task<Result<IReadOnlyList<AsistentesDetalle>>> LeerAsync(MensajeCompraDto mensaje, CancellationToken ct);
}
