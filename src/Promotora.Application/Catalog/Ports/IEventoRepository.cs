using Promotora.Application.Shared;
using Promotora.Domain.Catalog;

namespace Promotora.Application.Catalog.Ports;

public interface IEventoRepository
{
    Task<Evento?> FindAsync(int id, bool bloquear, CancellationToken ct);
    Task<Evento> InsertAsync(Evento evento, CancellationToken ct);
    Task UpdateAsync(Evento evento, CancellationToken ct);
    Task<bool> ChangeStateAsync(int id, EstadoEvento esperado, EstadoEvento nuevo, CancellationToken ct);
    Task<bool> TieneComprasAsync(int eventoId, CancellationToken ct);
    Task<long> CupoDisponibleAsync(int eventoId, CancellationToken ct);
    Task<PaginaDto<EventoDto>> ListAsync(int? duenoId, int page, int pageSize, CancellationToken ct);
}
