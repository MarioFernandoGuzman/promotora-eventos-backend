using Promotora.Application.Finance.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Finance;

public sealed record AmbitoConsultaCuenta(int? DuenoId);
public sealed class ConsultaCuentaAccess(IUsuarioActual actual, IUsuarioRepository usuarios, ISesionRepository sesiones, ICuentaRepository cuentas)
{
    public static Error NotFound { get; } = new("cuenta.no_encontrada", "Cuenta no encontrada.", ErrorType.NotFound);
    private static Error Forbidden { get; } = new("cuenta.prohibida", "Se requiere un administrador o dueño habilitado.", ErrorType.Forbidden);
    public async Task<Result<AmbitoConsultaCuenta>> AutorizarAsync(CancellationToken ct)
    {
        if (actual.UsuarioId is not int id) return Result<AmbitoConsultaCuenta>.Failure(Forbidden);
        await sesiones.LockUsuarioAsync(id, ct);
        var actor = await usuarios.FindByIdAsync(id, ct);
        if (actor is null || !actor.PuedeAutenticarse || actor.DebeCambiarPassword)
            return Result<AmbitoConsultaCuenta>.Failure(Forbidden);
        if (actor.Rol == RolUsuario.ADMINISTRADOR) return Result<AmbitoConsultaCuenta>.Success(new(null));
        if (actor.Rol != RolUsuario.DUENO_RECINTO || await cuentas.DuenoIdAsync(id, ct) is not int dueno)
            return Result<AmbitoConsultaCuenta>.Failure(Forbidden);
        return Result<AmbitoConsultaCuenta>.Success(new(dueno));
    }
}
public sealed class CuentasHandler(ICuentaConsultaRepository cuentas, ConsultaCuentaAccess access, IUnitOfWork unitOfWork)
    : IQueryHandler<CuentasQuery, PaginaDto<CuentaDto>>
{
    public Task<Result<PaginaDto<CuentaDto>>> HandleAsync(CuentasQuery query, CancellationToken ct) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        var permiso = await access.AutorizarAsync(token);
        if (permiso.IsFailure) return Result<PaginaDto<CuentaDto>>.Failure(permiso.Errors);
        return Result<PaginaDto<CuentaDto>>.Success(await cuentas.ListarAsync(query, permiso.Value.DuenoId, token));
    }, ct);
}
public sealed class ObtenerCuentaHandler(ICuentaConsultaRepository cuentas, ConsultaCuentaAccess access, IUnitOfWork unitOfWork)
    : IQueryHandler<ObtenerCuentaQuery, CuentaDto>
{
    public Task<Result<CuentaDto>> HandleAsync(ObtenerCuentaQuery query, CancellationToken ct) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        var permiso = await access.AutorizarAsync(token);
        if (permiso.IsFailure) return Result<CuentaDto>.Failure(permiso.Errors);
        var cuenta = await cuentas.ObtenerAsync(query.Id, permiso.Value.DuenoId, token);
        return cuenta is null ? Result<CuentaDto>.Failure(ConsultaCuentaAccess.NotFound) : Result<CuentaDto>.Success(cuenta);
    }, ct);
}
public sealed class MovimientosCuentaHandler(ICuentaConsultaRepository cuentas, ConsultaCuentaAccess access, IUnitOfWork unitOfWork)
    : IQueryHandler<MovimientosCuentaQuery, PaginaDto<MovimientoCuentaDto>>
{
    public Task<Result<PaginaDto<MovimientoCuentaDto>>> HandleAsync(MovimientosCuentaQuery query, CancellationToken ct) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        var permiso = await access.AutorizarAsync(token);
        if (permiso.IsFailure) return Result<PaginaDto<MovimientoCuentaDto>>.Failure(permiso.Errors);
        if (await cuentas.ObtenerAsync(query.Id, permiso.Value.DuenoId, token) is null)
            return Result<PaginaDto<MovimientoCuentaDto>>.Failure(ConsultaCuentaAccess.NotFound);
        return Result<PaginaDto<MovimientoCuentaDto>>.Success(await cuentas.MovimientosAsync(query, token));
    }, ct);
}
