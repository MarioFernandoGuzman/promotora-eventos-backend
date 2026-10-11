using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Finance;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Finance.Ports;

public sealed record CompraParaCobro(int Id, Guid PublicId, int DuenoId, string Moneda, string Estado, ImportesCompra Importes);
public sealed record CompraCompletadaDto(Guid PublicId, string Estado, int Boletos);

// Orquestación interna, sin endpoint ni llamada a la pasarela dentro de la transacción.
public interface ICompletarCompraPort
{
    Task<Result<CompraCompletadaDto>> CompletarAsync(Guid publicId, long intentoId, RespuestaPago respuesta,
        IReadOnlyList<AsistentesDetalle> asistentes, CancellationToken ct);
}

public interface ICobroRepository
{
    Task<Result<CompraParaCobro>> LockCompraAsync(Guid publicId, CancellationToken ct);
    Task<IReadOnlyList<CuentaCobro>> LockCuentasAsync(int duenoId, string moneda, CancellationToken ct);
    Task<bool> TieneMovimientosAsync(int compraId, CancellationToken ct);
    Task<bool> CuentasConciliadasAsync(IReadOnlyList<int> ids, CancellationToken ct);
    Task<IReadOnlyList<long>> RegistrarMovimientosAsync(int compraId, IReadOnlyList<MovimientoCobro> movimientos, DateTime fecha, CancellationToken ct);
    Task<bool> MarcarCompletadaAsync(int compraId, CancellationToken ct);
    Task<bool> EmisionYOutboxCompletosAsync(CompraParaCobro compra, CancellationToken ct);
}
