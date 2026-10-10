using Promotora.Application.Shared;

namespace Promotora.Application.Catalog.Ports;

public interface ICatalogoPublicoQueryPort
{
    Task<PaginaDto<EventoResumenPublicoDto>> ListAsync(ListarCatalogoPublicoQuery request, CancellationToken ct);
    Task<EventoPublicoDto?> FindAsync(int id, CancellationToken ct);
}
