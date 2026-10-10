using Promotora.Domain.Shared;

namespace Promotora.Application.Finance.Ports;

// Referencia = compras.public_id. No acepta datos de tarjeta.
public sealed record SolicitudPago(Guid Referencia, decimal Monto, string Moneda);
public sealed record RespuestaPago(Guid Referencia, decimal Monto, string Moneda, string Pasarela, string Estado, DateTime Fecha);

public interface IPaymentGateway
{
    Task<Result<RespuestaPago>> CobrarAsync(SolicitudPago solicitud, CancellationToken ct);
    Task<RespuestaPago?> ConsultarAsync(Guid referencia, CancellationToken ct);
}
