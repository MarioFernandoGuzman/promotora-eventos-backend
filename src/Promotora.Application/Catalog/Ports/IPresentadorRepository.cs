using Promotora.Application.Shared;
using Promotora.Domain.Catalog;

namespace Promotora.Application.Catalog.Ports;

public interface IPresentadorRepository
{
    Task<Presentador?> FindAsync(int id, bool bloquear, CancellationToken ct);
    Task<Presentador> InsertAsync(Presentador entity, CancellationToken ct);
    Task UpdateAsync(Presentador entity, CancellationToken ct);
    Task<bool> DeactivateAsync(int id, CancellationToken ct);
    Task<PaginaDto<PresentadorDto>> ListAsync(int page, int pageSize, CancellationToken ct);
}
