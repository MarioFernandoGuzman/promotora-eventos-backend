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

public sealed record CerrarCuentaCommand(int Id) : ICommand<CuentaDto>;
public sealed record CuentaDto(int Id, string Tipo, int? DuenoId, string Moneda, decimal Saldo, string Estado, DateTime? FechaCierre);
public sealed class CerrarCuentaValidator : AbstractValidator<CerrarCuentaCommand>
{
    public CerrarCuentaValidator() => RuleFor(c => c.Id).GreaterThan(0);
}

public sealed class CerrarCuentaHandler(ICuentaRepository cuentas, IUsuarioActual actual, IUsuarioRepository usuarios,
    ISesionRepository sesiones, IUnitOfWork unitOfWork, IClock clock, IAuditLogger audit) : ICommandHandler<CerrarCuentaCommand, CuentaDto>
{
    public Task<Result<CuentaDto>> HandleAsync(CerrarCuentaCommand request, CancellationToken ct) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        if (actual.UsuarioId is not int actorId) return Fallo("cuenta.prohibida", "Se requiere un administrador o dueño habilitado.", ErrorType.Forbidden);
        await sesiones.LockUsuarioAsync(actorId, token);
        var actor = await usuarios.FindByIdAsync(actorId, token);
        if (actor is null || !actor.PuedeAutenticarse || actor.DebeCambiarPassword || actor.Rol is not (RolUsuario.ADMINISTRADOR or RolUsuario.DUENO_RECINTO))
            return Fallo("cuenta.prohibida", "Se requiere un administrador o dueño habilitado.", ErrorType.Forbidden);
        var cuenta = await cuentas.LockAsync(request.Id, token);
        if (cuenta is null || (actor.Rol == RolUsuario.DUENO_RECINTO && (cuenta.Tipo != "DUENO"
            || cuenta.DuenoId != await cuentas.DuenoIdAsync(actorId, token))))
            return Fallo("cuenta.no_encontrada", "Cuenta no encontrada.", ErrorType.NotFound);
        bool yaCerrada = cuenta.Estado == "CERRADA";
        var cierre = cuenta.Cerrar(clock.UtcNow);
        if (cierre.IsFailure) return Result<CuentaDto>.Failure(cierre.Errors);
        if (!yaCerrada)
        {
            if (!await cuentas.CerrarAsync(cuenta.Id, cuenta.FechaCierre!.Value, token))
                return Fallo("cuenta.conflicto", "La cuenta cambió de estado o saldo.", ErrorType.Conflict);
            await audit.WriteAsync(actorId, "UPDATE", "cuentas_financieras", cuenta.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
            cuenta = await cuentas.LockAsync(cuenta.Id, token)
                ?? throw new InvalidOperationException("La cuenta cerrada no está disponible en su transacción.");
        }
        return Result<CuentaDto>.Success(ToDto(cuenta));
    }, ct);

    private static CuentaDto ToDto(CuentaFinanciera c) => new(c.Id, c.Tipo, c.DuenoId, c.Moneda, c.Saldo, c.Estado, c.FechaCierre);
    private static Result<CuentaDto> Fallo(string codigo, string mensaje, ErrorType tipo) => Result<CuentaDto>.Failure(new Error(codigo, mensaje, tipo));
}
