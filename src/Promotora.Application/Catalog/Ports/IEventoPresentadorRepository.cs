using Promotora.Application.Shared;

namespace Promotora.Application.Catalog.Ports;

public interface IEventoPresentadorRepository
{
    Task<EventoPresentadorDto?> FindAsync(int eventoId, int presentadorId, CancellationToken ct);
    Task InsertAsync(int eventoId, int presentadorId, string? rol, CancellationToken ct);
    Task UpdateRoleAsync(int eventoId, int presentadorId, string? rol, CancellationToken ct);
    Task<bool> DeleteAsync(int eventoId, int presentadorId, CancellationToken ct);
    Task<PaginaDto<EventoPresentadorDto>> ListAsync(int eventoId, int page, int pageSize, CancellationToken ct);
}
