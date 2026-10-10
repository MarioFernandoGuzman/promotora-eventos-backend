using Promotora.Domain.Venues;

namespace Promotora.Application.Venues.Ports;

public interface IRecintoRepository
{
    Task<int?> DuenoIdAsync(int usuarioId, CancellationToken ct);
    Task<bool> DuenoActivoAsync(int duenoId, CancellationToken ct);
    Task<Recinto?> FindAsync(int id, bool bloquear, CancellationToken ct);
    Task<Recinto> InsertAsync(Recinto recinto, CancellationToken ct);
    Task UpdateAsync(Recinto recinto, CancellationToken ct);
    Task<bool> DeactivateAsync(int id, CancellationToken ct);
    Task<(long Total, bool TieneLocalidades)> CapacidadAsync(int recintoId, CancellationToken ct);
    Task<RecintosPaginaDto> ListAsync(int? duenoId, int page, int pageSize, CancellationToken ct);
}
