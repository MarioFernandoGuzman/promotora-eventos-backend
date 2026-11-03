using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class CambiarEstadoEventoHandler(IEventoRepository eventos, ICategoriaEventoRepository categorias, ICatalogTransaction transaction,
    EventoAccess access, IUsuarioActual actual, IAuditLogger audit, IClock clock) : ICommandHandler<CambiarEstadoEventoCommand, EventoDto>
{
    public Task<Result<EventoDto>> HandleAsync(CambiarEstadoEventoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var result = await access.FindAsync(request.Id, true, request.EstadoNuevo != "CANCELADO", token);
        if (result.IsFailure) return Result<EventoDto>.Failure(result.Errors);
        var expected = Enum.Parse<EstadoEvento>(request.EstadoEsperado);
        var next = Enum.Parse<EstadoEvento>(request.EstadoNuevo);
        if (next == EstadoEvento.CANCELADO && await eventos.TieneComprasAsync(request.Id, token))
            return Result<EventoDto>.Failure(new Error("catalog.compras", "La cancelación con compras requiere el flujo de reembolso; no se cancela desde este CRUD.", ErrorType.Conflict));
        if (next is EstadoEvento.PROGRAMADO or EstadoEvento.EN_VENTA)
        {
            var category = await categorias.FindAsync(result.Value.Datos.CategoriaId, true, token);
            if (category is null || !category.Activo) return Result<EventoDto>.Failure(CatalogAccess.Inactivo);
        }
        var change = result.Value.CambiarEstado(expected, next, clock.UtcNow, await eventos.CupoDisponibleAsync(request.Id, token));
        if (change.IsFailure) return Result<EventoDto>.Failure(change.Errors);
        if (!await eventos.ChangeStateAsync(request.Id, expected, next, token)) return Result<EventoDto>.Failure(new Error("catalog.concurrent", "El estado esperado ya cambió.", ErrorType.Conflict));
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "eventos", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<EventoDto>.Success(EventoDto.From(result.Value));
    }, ct);
}

public sealed class CancelarEventoHandler(IEventoRepository eventos, ICatalogTransaction transaction,
    EventoAccess access, IUsuarioActual actual, IAuditLogger audit, IClock clock) : ICommandHandler<CancelarEventoCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(CancelarEventoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var result = await access.FindAsync(request.Id, true, false, token);
        if (result.IsFailure) return Result<Unit>.Failure(result.Errors);
        var evento = result.Value;
        if (evento.Estado == EstadoEvento.CANCELADO) return Result<Unit>.Success(Unit.Value);
        if (await eventos.TieneComprasAsync(request.Id, token)) return Result<Unit>.Failure(new Error("catalog.compras", "La cancelación con compras requiere el flujo de reembolso; no se cancela desde este CRUD.", ErrorType.Conflict));
        var expected = evento.Estado;
        var change = evento.CambiarEstado(expected, EstadoEvento.CANCELADO, clock.UtcNow, 0);
        if (change.IsFailure) return Result<Unit>.Failure(change.Errors);
        if (await eventos.ChangeStateAsync(request.Id, expected, EstadoEvento.CANCELADO, token))
            await audit.WriteAsync(actual.UsuarioId, "DELETE", "eventos", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
