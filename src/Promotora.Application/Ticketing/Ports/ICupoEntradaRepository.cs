using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing.Ports;

// Ambas operaciones requieren la transacción de negocio del llamador.
public interface ICupoEntradaRepository
{
    Task<bool> ReservarAsync(int eventoId, int tipoEntradaId, int cantidad, CancellationToken ct);
    Task<bool> FinalizarYDevolverAsync(int compraId, EstadoCompra esperado, EstadoCompra siguiente, DateTime ahora, CancellationToken ct);
}
