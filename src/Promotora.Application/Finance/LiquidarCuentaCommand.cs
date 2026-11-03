using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Promotora.Application.Audit;
using Promotora.Application.Finance.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Finance;

public sealed record LiquidarCuentaCommand(int Id, decimal Monto, string IdempotencyKey) : ICommand<LiquidacionDto>;
public sealed record LiquidacionDto(long MovimientoId, int CuentaId, decimal Monto, string Moneda, DateTime Fecha);
public sealed class LiquidarCuentaValidator : AbstractValidator<LiquidarCuentaCommand>
{
    public LiquidarCuentaValidator()
    {
        RuleFor(c => c.Id).GreaterThan(0);
        RuleFor(c => c.Monto).GreaterThan(0m).LessThanOrEqualTo(9999999999.99m).Must(m => decimal.Round(m, 2) == m)
            .WithMessage("El monto admite máximo dos decimales.");
        RuleFor(c => c.IdempotencyKey).NotEmpty().MaximumLength(128).Matches("\\A[A-Za-z0-9._:-]+\\z");
    }
}

public sealed class LiquidarCuentaHandler(ICuentaRepository cuentas, ILiquidacionRepository liquidaciones, IUsuarioActual actual,
    IUsuarioRepository usuarios, ISesionRepository sesiones, IUnitOfWork unitOfWork, IClock clock, IAuditLogger audit)
    : ICommandHandler<LiquidarCuentaCommand, LiquidacionDto>
{
    public Task<Result<LiquidacionDto>> HandleAsync(LiquidarCuentaCommand request, CancellationToken ct) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        if (actual.UsuarioId is not int actorId) return Fallo("liquidacion.prohibida", "Se requiere un administrador habilitado.", ErrorType.Forbidden);
        await sesiones.LockUsuarioAsync(actorId, token);
        var actor = await usuarios.FindByIdAsync(actorId, token);
        if (actor is null || !actor.PuedeAutenticarse || actor.DebeCambiarPassword || actor.Rol != RolUsuario.ADMINISTRADOR)
            return Fallo("liquidacion.prohibida", "Se requiere un administrador habilitado.", ErrorType.Forbidden);
        var cuenta = await cuentas.LockAsync(request.Id, token);
        if (cuenta is null) return Fallo("liquidacion.no_encontrada", "Cuenta no encontrada.", ErrorType.NotFound);
        // La referencia se conserva en el ledger existente. El bloqueo serializa su búsqueda e inserción por cuenta.
        string referencia = "liquidacion:v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.IdempotencyKey)));
        var previa = await liquidaciones.FindAsync(cuenta.Id, referencia, token);
        if (previa is not null)
            return previa.Monto == request.Monto ? Result<LiquidacionDto>.Success(previa)
                : Fallo("liquidacion.idempotencia", "La clave ya fue usada con otro monto en esta cuenta.", ErrorType.Conflict);
        var movimiento = cuenta.Liquidar(request.Monto);
        if (movimiento.IsFailure) return Result<LiquidacionDto>.Failure(movimiento.Errors);
        if (await liquidaciones.SumaMovimientosAsync(cuenta.Id, token) != cuenta.Saldo)
            return Fallo("liquidacion.conciliacion", "El saldo no coincide con los movimientos registrados.", ErrorType.Conflict);
        var registrada = await liquidaciones.RegistrarAsync(cuenta.Id, request.Monto, referencia, clock.UtcNow, token);
        if (registrada is null) return Fallo("liquidacion.conflicto", "La cuenta cambió de estado o saldo.", ErrorType.Conflict);
        await audit.WriteAsync(actorId, "CREATE", "movimientos_financieros", registrada.MovimientoId.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        await audit.WriteAsync(actorId, "UPDATE", "cuentas_financieras", cuenta.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<LiquidacionDto>.Success(registrada);
    }, ct);

    private static Result<LiquidacionDto> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<LiquidacionDto>.Failure(new Error(codigo, mensaje, tipo));
}
