using Promotora.Application.Shared;
using Promotora.Domain.Catalog;

namespace Promotora.Application.Catalog.Ports;

public interface ITipoEntradaRepository
{
    Task<TipoEntrada?> FindAsync(int eventoId, int id, bool bloquear, CancellationToken ct);
    Task<TipoEntrada> InsertAsync(TipoEntrada tipo, CancellationToken ct);
    Task<bool> UpdateAsync(TipoEntrada tipo, int cupoAnterior, CancellationToken ct);
    Task<bool> DeactivateAsync(int eventoId, int id, CancellationToken ct);
    Task<long> OtrosCuposAsync(int eventoId, int localidadId, int? excluirId, CancellationToken ct);
    Task<PaginaDto<TipoEntradaDto>> ListAsync(int eventoId, int page, int pageSize, CancellationToken ct);
}
