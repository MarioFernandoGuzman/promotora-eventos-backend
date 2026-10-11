namespace Promotora.Application.Ticketing.Ports;

public interface IRecuperacionCompraRepository
{
    Task<IReadOnlyList<MensajeCompraDto>> CandidatasAsync(DateTime limite, int lote, CancellationToken ct);
    // Revalida timeout y estado bajo lock; null = no elegible o bloqueada por otro proceso.
    Task<CompraTomadaDto?> LockAsync(Guid publicId, DateTime limite, CancellationToken ct);
    Task<bool> TieneEfectosConfirmadosAsync(int compraId, CancellationToken ct);
}
