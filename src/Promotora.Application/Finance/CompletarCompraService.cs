using System.Globalization;
using System.Text.Json;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Shared.Messaging;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Finance;
using Promotora.Domain.Shared;

namespace Promotora.Application.Finance;

public sealed class CompletarCompraService(ICobroRepository repository, IRegistroPagoPort pagos, IAltaCuentaDuenoPort altas,
    IEmisionBoletosPort emision, IEmisionBoletosRepository respaldo, IOutboxWriter outbox, IUnitOfWork unitOfWork,
    IClock clock, IAuditLogger audit) : ICompletarCompraPort
{
    public Task<Result<CompraCompletadaDto>> CompletarAsync(Guid publicId, long intentoId, RespuestaPago respuesta,
        IReadOnlyList<AsistentesDetalle> asistentes, CancellationToken ct)
    {
        if (publicId == Guid.Empty || intentoId <= 0 || respuesta is null || asistentes is null)
            return Task.FromResult(Fallo("finanzas.solicitud", "Solicitud de finalización inválida.", ErrorType.Validation));
        if (respuesta.Referencia != publicId || respuesta.Estado != "APROBADO")
            return Task.FromResult(Fallo("finanzas.pago", "Solo se completa con pago APROBADO de esta compra.", ErrorType.Conflict));
        return unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var encontrado = await repository.LockCompraAsync(publicId, token);
            if (encontrado.IsFailure) return Result<CompraCompletadaDto>.Failure(encontrado.Errors);
            var compra = encontrado.Value;
            if (compra.Estado is not ("PROCESANDO" or "COMPLETADA"))
                return Fallo("finanzas.estado", "La compra ya no admite finalización.", ErrorType.Conflict);
            var pago = await pagos.ResolverEnTransaccionAsync(intentoId, respuesta, token);
            if (pago.IsFailure) return Result<CompraCompletadaDto>.Failure(pago.Errors);
            if (pago.Value.CompraId != compra.Id) return Fallo("finanzas.pago", "El intento pertenece a otra compra.", ErrorType.Conflict);
            if (compra.Estado == "COMPLETADA")
            {
                var paraEmision = await respaldo.LockCompraAsync(publicId, token);
                if (paraEmision is null || !(await respaldo.RespaldoAsync(paraEmision, token)).PermiteEmision(compra.Importes)
                    || !await repository.EmisionYOutboxCompletosAsync(compra, token))
                    return Fallo("finanzas.inconsistente", "La finalización existente requiere conciliación.", ErrorType.Conflict);
                var existentes = await emision.EnTransaccionAsync(publicId, asistentes, token);
                return existentes.IsSuccess ? Result<CompraCompletadaDto>.Success(new(publicId, "COMPLETADA", existentes.Value.Count))
                    : Result<CompraCompletadaDto>.Failure(existentes.Errors);
            }
            if (await repository.TieneMovimientosAsync(compra.Id, token))
                return Fallo("finanzas.inconsistente", "Una compra PROCESANDO ya tiene movimientos; requiere conciliación.", ErrorType.Conflict);
            IReadOnlyList<CuentaCobro> cuentas = [];
            if (compra.Importes.RequiereMovimientos)
            {
                if (compra.Moneda != "GTQ") return Fallo("finanzas.moneda", "El catálogo actual usa GTQ.", ErrorType.Conflict);
                await altas.CrearEnTransaccionAsync(compra.DuenoId, null, null, token);
                cuentas = await repository.LockCuentasAsync(compra.DuenoId, compra.Moneda, token);
            }
            var plan = MovimientosCobro.Crear(compra.Importes, compra.DuenoId, compra.Moneda, cuentas);
            if (plan.IsFailure) return Result<CompraCompletadaDto>.Failure(plan.Errors);
            var ids = plan.Value.Select(m => m.CuentaId).ToArray();
            if (!await repository.CuentasConciliadasAsync(ids, token))
                return Fallo("finanzas.saldo", "El saldo no coincide con el ledger.", ErrorType.Conflict);
            var movimientos = await repository.RegistrarMovimientosAsync(compra.Id, plan.Value, clock.UtcNow, token);
            foreach (long id in movimientos)
                await audit.WriteAsync(null, "CREATE", "movimientos_financieros", id.ToString(CultureInfo.InvariantCulture), null, token);
            if (!await repository.MarcarCompletadaAsync(compra.Id, token))
                return Fallo("finanzas.estado", "La compra perdió la transición a COMPLETADA.", ErrorType.Conflict);
            var emitidos = await emision.EnTransaccionAsync(publicId, asistentes, token);
            if (emitidos.IsFailure) return Result<CompraCompletadaDto>.Failure(emitidos.Errors);
            await audit.WriteAsync(null, "UPDATE", "compras", compra.Id.ToString(CultureInfo.InvariantCulture), null, token);
            using var payload = JsonSerializer.SerializeToDocument(new { publicId });
            await outbox.EscribirAsync(new NuevoMensajeOutbox("Compra", publicId.ToString("D"), "compra.completada", payload.RootElement), token);
            return Result<CompraCompletadaDto>.Success(new(publicId, "COMPLETADA", emitidos.Value.Count));
        }, ct);
    }

    private static Result<CompraCompletadaDto> Fallo(string codigo, string mensaje, ErrorType tipo) =>
        Result<CompraCompletadaDto>.Failure(new Error(codigo, mensaje, tipo));
}
