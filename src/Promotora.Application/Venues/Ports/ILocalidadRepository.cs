using Promotora.Domain.Venues;

namespace Promotora.Application.Venues.Ports;

public interface ILocalidadRepository
{
    Task<Localidad?> FindAsync(int recintoId, int id, bool bloquear, CancellationToken ct);
    Task<Localidad> InsertAsync(Localidad localidad, CancellationToken ct);
    Task UpdateAsync(Localidad localidad, CancellationToken ct);
    Task<bool> DeactivateAsync(int recintoId, int id, CancellationToken ct);
    Task<long> CapacidadOtrasAsync(int recintoId, int? excluirId, CancellationToken ct);
    Task<long> MaxCupoEventoAsync(int localidadId, CancellationToken ct);
    Task<LocalidadesPaginaDto> ListAsync(int recintoId, int page, int pageSize, CancellationToken ct);
}
