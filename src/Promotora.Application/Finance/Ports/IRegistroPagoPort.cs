using Promotora.Domain.Shared;

namespace Promotora.Application.Finance.Ports;

public sealed record PagoRegistradoDto(long Id, int CompraId, Guid Referencia, decimal Monto,
    string Moneda, string Pasarela, string Estado, DateTime Fecha);

// Ambos métodos participan en la transacción del orquestador de compras; no cobran por sí solos.
public interface IRegistroPagoPort
{
    Task<Result<PagoRegistradoDto>> IniciarEnTransaccionAsync(Guid compraPublicId, string pasarela, CancellationToken ct);
    Task<Result<PagoRegistradoDto>> ResolverEnTransaccionAsync(long intentoId, RespuestaPago respuesta, CancellationToken ct);
}
