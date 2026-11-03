using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Infrastructure.Finance;

public sealed record FakePaymentOptions(decimal TasaFallo = 0m);

// Simulación sin cobros externos. La consulta conserva resultados durante la vida del host.
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<Guid, Lazy<RespuestaPago>> _pagos = new();
    private readonly FakePaymentOptions _options;
    private readonly IClock _clock;

    public FakePaymentGateway(FakePaymentOptions options, IClock clock)
    {
        if (options.TasaFallo is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(options), "La tasa de fallo debe estar entre 0 y 1.");
        _options = options; _clock = clock;
    }

    public Task<Result<RespuestaPago>> CobrarAsync(SolicitudPago solicitud, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.Referencia == Guid.Empty || !ImporteValido(solicitud.Monto) || solicitud.Moneda != "GTQ")
            return Task.FromResult(Result<RespuestaPago>.Failure(new Error("pago.datos", "Referencia, monto o moneda inválidos.", ErrorType.Validation)));
        var pago = _pagos.GetOrAdd(solicitud.Referencia, _ => new Lazy<RespuestaPago>(() => Simular(solicitud), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if (pago.Monto != solicitud.Monto || pago.Moneda != solicitud.Moneda)
            return Task.FromResult(Result<RespuestaPago>.Failure(new Error("pago.referencia", "La referencia ya tiene otros importes.", ErrorType.Conflict)));
        return Task.FromResult(Result<RespuestaPago>.Success(pago));
    }

    public Task<RespuestaPago?> ConsultarAsync(Guid referencia, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_pagos.TryGetValue(referencia, out var pago) ? pago.Value : null);
    }

    private RespuestaPago Simular(SolicitudPago solicitud)
    {
        var hash = SHA256.HashData(solicitud.Referencia.ToByteArray());
        decimal muestra = BinaryPrimitives.ReadUInt32BigEndian(hash) / 4294967296m;
        // Gratuitas no fallan: no hay un cobro externo que rechazar.
        bool rechazado = solicitud.Monto > 0m && muestra < _options.TasaFallo;
        return new(solicitud.Referencia, solicitud.Monto, solicitud.Moneda, "SIMULADA", rechazado ? "RECHAZADO" : "APROBADO", _clock.UtcNow);
    }

    private static bool ImporteValido(decimal monto) => monto is >= 0m and <= ImportesCompra.Maximo
        && decimal.Round(monto, 2, MidpointRounding.AwayFromZero) == monto;
}
