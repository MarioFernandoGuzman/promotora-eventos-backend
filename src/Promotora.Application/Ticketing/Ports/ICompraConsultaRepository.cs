using Promotora.Application.Shared;

namespace Promotora.Application.Ticketing.Ports;

public sealed record CompraPropia(int Id, CompraCreadaDto Compra);
public interface ICompraConsultaRepository
{
    Task<CompraPropia?> ObtenerAsync(Guid publicId, int clienteId, CancellationToken ct);
    Task<PaginaDto<CompraCreadaDto>> MisComprasAsync(int clienteId, int page, int pageSize, CancellationToken ct);
    Task<PaginaDto<BoletoClienteDto>> MisBoletosAsync(int clienteId, int page, int pageSize, CancellationToken ct);
}
