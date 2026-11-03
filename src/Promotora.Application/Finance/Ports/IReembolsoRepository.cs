using Promotora.Domain.Finance;
using Promotora.Domain.Shared;

namespace Promotora.Application.Finance.Ports;

public sealed record PagoReembolso(long Id, string Estado, string? Referencia, decimal Monto);
public sealed record BoletoAnulado(long Id, int TipoEntradaId);

public interface IReembolsoRepository
{
    Task<IReadOnlyList<PagoReembolso>> PagosAsync(int compraId, CancellationToken ct);
    Task<IReadOnlyList<MovimientoOriginal>> LedgerAsync(int compraId, CancellationToken ct);
    Task<IReadOnlyList<CuentaCobro>> LockCuentasAsync(IReadOnlyList<int> ids, CancellationToken ct);
    Task<bool> BoletosCompletosAsync(int compraId, bool reembolsada, CancellationToken ct);
    Task<bool> MarcarPagoAsync(long pagoId, CancellationToken ct);
    Task<Result<IReadOnlyList<BoletoAnulado>>> AnularYDevolverAsync(int compraId, DateTime ahora, CancellationToken ct);
}
