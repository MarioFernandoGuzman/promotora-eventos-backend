using System.Globalization;
using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Finance;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Finance;

public sealed record ReembolsarCompraCommand(Guid PublicId) : ICommand<ReembolsoDto>;
public sealed record ReembolsoDto(Guid PublicId, string EstadoPago, decimal MontoReembolsado, string Moneda);
public sealed class ReembolsarCompraValidator : AbstractValidator<ReembolsarCompraCommand>
{
    public ReembolsarCompraValidator() => RuleFor(c => c.PublicId).NotEmpty();
}

public sealed class ReembolsarCompraHandler(IReembolsoRepository repository, ICobroRepository cobros,
    IUsuarioActual actual, IUsuarioRepository usuarios, ISesionRepository sesiones, IUnitOfWork unitOfWork,
    IClock clock, IAuditLogger audit) : ICommandHandler<ReembolsarCompraCommand, ReembolsoDto>
{
    public Task<Result<ReembolsoDto>> HandleAsync(ReembolsarCompraCommand request, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (request.PublicId == Guid.Empty) return Fallo("reembolso.id", "PublicId requerido.", ErrorType.Validation);
            if (actual.UsuarioId is not int actorId) return Prohibido();
            await sesiones.LockUsuarioAsync(actorId, token);
            var actor = await usuarios.FindByIdAsync(actorId, token);
            if (actor is null || actor.Rol != RolUsuario.ADMINISTRADOR || !actor.PuedeAutenticarse || actor.DebeCambiarPassword) return Prohibido();
            var encontrado = await cobros.LockCompraAsync(request.PublicId, token);
            if (encontrado.IsFailure) return Result<ReembolsoDto>.Failure(encontrado.Errors);
            var compra = encontrado.Value;
            if (compra.Estado != "COMPLETADA") return Conflicto();
            var pagos = await repository.PagosAsync(compra.Id, token);
            if (pagos.Count != 1 || pagos[0].Referencia != compra.PublicId.ToString("D") || pagos[0].Monto != compra.Importes.Total) return Conflicto();
            var pago = pagos[0]; bool repetida = pago.Estado == "REEMBOLSADO";
            var ledger = await repository.LedgerAsync(compra.Id, token);
            var originales = ledger.Where(m => m.Tipo is "VENTA" or "COMISION" or "IMPUESTO").ToArray();
            var devoluciones = ledger.Where(m => m.Tipo == "REEMBOLSO").ToArray();
            if (ledger.Count != originales.Length + devoluciones.Length) return Conflicto();
            var cuentas = await repository.LockCuentasAsync(originales.Select(m => m.CuentaId).Distinct().Order().ToArray(), token);
            var plan = MovimientosReembolso.Invertir(compra.Importes, compra.Moneda, originales, cuentas);
            if (plan.IsFailure) return Result<ReembolsoDto>.Failure(plan.Errors);
            if (!await cobros.CuentasConciliadasAsync(cuentas.Select(c => c.Id).ToArray(), token)
                || !await repository.BoletosCompletosAsync(compra.Id, repetida, token)) return Conflicto();
            if (repetida)
            {
                if (devoluciones.Length != plan.Value.Count || plan.Value.Any(m => devoluciones.Count(d => d.CuentaId == m.CuentaId && d.Monto == m.Monto) != 1)) return Conflicto();
                return Result<ReembolsoDto>.Success(new(compra.PublicId, "REEMBOLSADO", compra.Importes.Total, compra.Moneda));
            }
            if (devoluciones.Length != 0) return Conflicto();
            var disponible = MovimientosReembolso.ValidarDisponibilidad(plan.Value, cuentas);
            if (disponible.IsFailure) return Result<ReembolsoDto>.Failure(disponible.Errors);
            if (!await repository.MarcarPagoAsync(pago.Id, token)) return Conflicto();
            var ids = await cobros.RegistrarMovimientosAsync(compra.Id, plan.Value, clock.UtcNow, token);
            foreach (long id in ids) await audit.WriteAsync(actorId, "CREATE", "movimientos_financieros", id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            var anulados = await repository.AnularYDevolverAsync(compra.Id, token);
            if (anulados.IsFailure) return Result<ReembolsoDto>.Failure(anulados.Errors);
            foreach (var boleto in anulados.Value) await audit.WriteAsync(actorId, "UPDATE", "boletos", boleto.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            await audit.WriteAsync(actorId, "REFUND", "pagos", pago.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            await audit.WriteAsync(actorId, "REFUND", "compras", compra.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            return Result<ReembolsoDto>.Success(new(compra.PublicId, "REEMBOLSADO", compra.Importes.Total, compra.Moneda));
        }, ct);

    private static Result<ReembolsoDto> Prohibido() => Fallo("reembolso.prohibido", "Se requiere un administrador habilitado.", ErrorType.Forbidden);
    private static Result<ReembolsoDto> Conflicto() => Fallo("reembolso.conflicto", "El estado o respaldo de la compra no admite reembolso.", ErrorType.Conflict);
    private static Result<ReembolsoDto> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<ReembolsoDto>.Failure(new Error(codigo, mensaje, tipo));
}
