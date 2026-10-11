using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Ticketing;

namespace Promotora.Application.Ticketing;

// La pasarela del proyecto es simulada, sin cobros externos. No publicar al broker desde aquí.
public sealed class ProcesarCompraService(ICompraMensajeRepository mensajes, IMensajeProcesadoRepository dedup,
    ICompraTomaRepository tomas, IRegistroPagoPort pagos, IPaymentGateway gateway, ICompletarCompraPort completar,
    ICupoEntradaRepository cupos, IUnitOfWork transaction, IClock clock, IAuditLogger audit) : IProcesarCompraMensajePort
{
    public Task<Result<Unit>> ProcesarAsync(MensajeCompraDto mensaje, CancellationToken ct)
        => transaction.ExecuteInTransactionAsync(async token =>
        {
            if (mensaje.MensajeId <= 0 || mensaje.PublicId == Guid.Empty)
                return Result<Unit>.Failure(new Error("compra.mensaje", "Identidad de mensaje inválida.", ErrorType.Validation));
            var asistentes = await mensajes.LeerAsync(mensaje, token);
            if (asistentes.IsFailure) return Result<Unit>.Failure(asistentes.Errors);
            if (!await dedup.RegistrarAsync(mensaje.MensajeId, "ProcesarCompraConsumer", token))
                return Result<Unit>.Success(Unit.Value);
            var compra = await tomas.TomarAsync(mensaje.PublicId, token);
            if (compra is null) return Result<Unit>.Success(Unit.Value);
            var intento = await pagos.IniciarEnTransaccionAsync(compra.PublicId, "SIMULADA", token);
            if (intento.IsFailure) return Result<Unit>.Failure(intento.Errors);
            var cobro = await gateway.CobrarAsync(new(compra.PublicId, compra.Total, compra.Moneda), token);
            if (cobro.IsFailure) return Result<Unit>.Failure(cobro.Errors);
            if (cobro.Value.Estado == "APROBADO")
            {
                var resultado = await completar.EnTransaccionAsync(compra.PublicId, intento.Value.Id, cobro.Value, asistentes.Value, token);
                return resultado.IsSuccess ? Result<Unit>.Success(Unit.Value) : Result<Unit>.Failure(resultado.Errors);
            }
            if (cobro.Value.Estado != "RECHAZADO")
                return Result<Unit>.Failure(new Error("compra.pago", "Resultado de pago desconocido.", ErrorType.Conflict));
            var rechazo = await pagos.ResolverEnTransaccionAsync(intento.Value.Id, cobro.Value, token);
            if (rechazo.IsFailure) return Result<Unit>.Failure(rechazo.Errors);
            if (!await cupos.FinalizarYDevolverAsync(compra.Id, EstadoCompra.PROCESANDO, EstadoCompra.FALLIDA, clock.UtcNow, token))
                return Result<Unit>.Failure(new Error("compra.estado", "La compra perdió la transición a FALLIDA.", ErrorType.Conflict));
            await audit.WriteAsync(null, "UPDATE", "compras", compra.Id.ToString(CultureInfo.InvariantCulture), null, token);
            return Result<Unit>.Success(Unit.Value);
        }, ct);
}
