using Promotora.Application.Shared;
using Promotora.Domain.Catalog;

namespace Promotora.Application.Catalog.Ports;

public interface ICategoriaEventoRepository
{
    Task<CategoriaEvento?> FindAsync(int id, bool bloquear, CancellationToken ct);
    Task<CategoriaEvento> InsertAsync(CategoriaEvento entity, CancellationToken ct);
    Task UpdateAsync(CategoriaEvento entity, CancellationToken ct);
    Task<bool> DeactivateAsync(int id, CancellationToken ct);
    Task<PaginaDto<CategoriaEventoDto>> ListAsync(int page, int pageSize, CancellationToken ct);
}
