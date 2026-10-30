using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Venues;
using Promotora.Domain.Shared;

namespace Promotora.Application.Ticketing.Numeracion;

public sealed class DistribucionAccess(IDistribucionRepository repository, VenuesAccess venues)
{
    public static Error NoEncontrada => new("numeracion.no_encontrada", "Distribución o localidad no encontrada.", ErrorType.NotFound);
    public async Task<Result<Unit>> ValidarAsync(int eventoId, int localidadId, bool bloquear, CancellationToken ct)
    {
        var actor = await venues.ActorAsync(bloquear, ct);
        if (actor is null) return Result<Unit>.Failure(VenuesAccess.Forbidden);
        var contexto = await repository.ContextoAsync(eventoId, localidadId, bloquear, ct);
        if (contexto is null || !VenuesAccess.Permite(actor, contexto.DuenoId)) return Result<Unit>.Failure(NoEncontrada);
        if (bloquear && !contexto.Activo) return Result<Unit>.Failure(new Error("numeracion.inactiva", "Evento o recinto inactivo.", ErrorType.Conflict));
        return Result<Unit>.Success(Unit.Value);
    }
}

public sealed class GuardarDistribucionHandler(IDistribucionRepository repository, DistribucionAccess access, IUnitOfWork uow,
    IAuditLogger audit, IUsuarioActual actual) : ICommandHandler<GuardarDistribucionCommand, DistribucionDto>
{
    public Task<Result<DistribucionDto>> HandleAsync(GuardarDistribucionCommand command, CancellationToken ct) => uow.ExecuteInTransactionAsync(async token =>
    {
        var permiso = await access.ValidarAsync(command.EventoId, command.LocalidadId, true, token);
        if (permiso.IsFailure) return Result<DistribucionDto>.Failure(permiso.Errors);
        var result = await repository.GuardarAsync(command, token);
        if (result.IsSuccess) await audit.WriteAsync(actual.UsuarioId, "UPDATE", "distribuciones_evento", result.Value.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return result;
    }, ct);
}
public sealed class PublicarDistribucionHandler(IDistribucionRepository repository, DistribucionAccess access, IUnitOfWork uow,
    IAuditLogger audit, IUsuarioActual actual) : ICommandHandler<PublicarDistribucionCommand, DistribucionDto>
{
    public Task<Result<DistribucionDto>> HandleAsync(PublicarDistribucionCommand command, CancellationToken ct) => uow.ExecuteInTransactionAsync(async token =>
    {
        var permiso = await access.ValidarAsync(command.EventoId, command.LocalidadId, true, token);
        if (permiso.IsFailure) return Result<DistribucionDto>.Failure(permiso.Errors);
        var result = await repository.PublicarAsync(command, token);
        if (result.IsSuccess) await audit.WriteAsync(actual.UsuarioId, "UPDATE", "distribuciones_evento", result.Value.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return result;
    }, ct);
}
public sealed class BloquearAsientosHandler(IDistribucionRepository repository, DistribucionAccess access, IUnitOfWork uow,
    IAuditLogger audit, IUsuarioActual actual) : ICommandHandler<BloquearAsientosCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(BloquearAsientosCommand command, CancellationToken ct) => uow.ExecuteInTransactionAsync(async token =>
    {
        var permiso = await access.ValidarAsync(command.EventoId, command.LocalidadId, true, token);
        if (permiso.IsFailure) return permiso;
        var result = await repository.BloquearAsync(command, token);
        if (result.IsSuccess) await audit.WriteAsync(actual.UsuarioId, "UPDATE", "distribuciones_evento", $"{command.EventoId}:{command.LocalidadId}", actual.IpOrigen, token);
        return result;
    }, ct);
}
public sealed class ObtenerDistribucionHandler(IDistribucionRepository repository, DistribucionAccess access) : IQueryHandler<ObtenerDistribucionQuery, DistribucionDto>
{
    public async Task<Result<DistribucionDto>> HandleAsync(ObtenerDistribucionQuery query, CancellationToken ct)
    {
        var permiso = await access.ValidarAsync(query.EventoId, query.LocalidadId, false, ct);
        if (permiso.IsFailure) return Result<DistribucionDto>.Failure(permiso.Errors);
        var dto = await repository.ObtenerAsync(query.EventoId, query.LocalidadId, false, query.Page, query.PageSize, null, ct);
        return dto is null ? Result<DistribucionDto>.Failure(DistribucionAccess.NoEncontrada) : Result<DistribucionDto>.Success(dto);
    }
}
public sealed class ListarAsientosHandler(IDistribucionRepository repository) : IQueryHandler<ListarAsientosQuery, DistribucionDto>
{
    public async Task<Result<DistribucionDto>> HandleAsync(ListarAsientosQuery query, CancellationToken ct)
    {
        var contexto = await repository.ContextoAsync(query.EventoId, query.LocalidadId, false, ct);
        if (contexto is not { Publico: true }) return Result<DistribucionDto>.Failure(DistribucionAccess.NoEncontrada);
        var dto = await repository.ObtenerAsync(query.EventoId, query.LocalidadId, true, query.Page, query.PageSize, query.MesaId, ct);
        return dto is null ? Result<DistribucionDto>.Failure(DistribucionAccess.NoEncontrada) : Result<DistribucionDto>.Success(dto);
    }
}
