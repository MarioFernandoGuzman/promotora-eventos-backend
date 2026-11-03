using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class CrearTipoEntradaHandler(ITipoEntradaRepository tipos, EventoConfigurationAccess access, TipoEntradaCapacity capacity,
    ICatalogTransaction transaction, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<CrearTipoEntradaCommand, TipoEntradaDto>
{
    public Task<Result<TipoEntradaDto>> HandleAsync(CrearTipoEntradaCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var evento = await access.FindAsync(request.EventoId, token);
        if (evento.IsFailure) return Result<TipoEntradaDto>.Failure(evento.Errors);
        var check = await capacity.ValidateAsync(evento.Value, request.LocalidadId, request.Datos.CupoTotal, null, token);
        if (check.IsFailure) return Result<TipoEntradaDto>.Failure(check.Errors);
        var tipo = await tipos.InsertAsync(new TipoEntrada(0, request.EventoId, request.LocalidadId, request.Datos with { Nombre = request.Datos.Nombre.Trim() }, request.Datos.CupoTotal, true), token);
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "tipos_entrada", tipo.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<TipoEntradaDto>.Success(TipoEntradaDto.From(tipo));
    }, ct);
}

public sealed class EditarTipoEntradaHandler(ITipoEntradaRepository tipos, EventoConfigurationAccess access, TipoEntradaCapacity capacity,
    ICatalogTransaction transaction, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<EditarTipoEntradaCommand, TipoEntradaDto>
{
    public Task<Result<TipoEntradaDto>> HandleAsync(EditarTipoEntradaCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var evento = await access.FindAsync(request.EventoId, token);
        if (evento.IsFailure) return Result<TipoEntradaDto>.Failure(evento.Errors);
        // Orden compartido: usuario → recinto → evento → localidad → tipo.
        var initial = await tipos.FindAsync(request.EventoId, request.Id, false, token);
        if (initial is null) return Result<TipoEntradaDto>.Failure(CatalogAccess.NotFound);
        var check = await capacity.ValidateAsync(evento.Value, initial.LocalidadId, request.Datos.CupoTotal, request.Id, token);
        if (check.IsFailure) return Result<TipoEntradaDto>.Failure(check.Errors);
        var tipo = await tipos.FindAsync(request.EventoId, request.Id, true, token);
        if (tipo is null) return Result<TipoEntradaDto>.Failure(CatalogAccess.NotFound);
        int anterior = tipo.Datos.CupoTotal;
        var edit = tipo.Editar(request.Datos with { Nombre = request.Datos.Nombre.Trim() });
        if (edit.IsFailure) return Result<TipoEntradaDto>.Failure(edit.Errors);
        if (!await tipos.UpdateAsync(tipo, anterior, token)) return Result<TipoEntradaDto>.Failure(new Error("catalog.concurrent", "El cupo o estado cambió concurrentemente.", ErrorType.Conflict));
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "tipos_entrada", tipo.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<TipoEntradaDto>.Success(TipoEntradaDto.From((await tipos.FindAsync(request.EventoId, request.Id, false, token))!));
    }, ct);
}

public sealed class BajaTipoEntradaHandler(ITipoEntradaRepository tipos, EventoConfigurationAccess access,
    ICatalogTransaction transaction, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<BajaTipoEntradaCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(BajaTipoEntradaCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var evento = await access.FindAsync(request.EventoId, token);
        if (evento.IsFailure) return Result<Unit>.Failure(evento.Errors);
        var tipo = await tipos.FindAsync(request.EventoId, request.Id, true, token);
        if (tipo is null) return Result<Unit>.Failure(CatalogAccess.NotFound);
        if (tipo.CupoDisponible != tipo.Datos.CupoTotal) return Result<Unit>.Failure(new Error("catalog.reservado", "El tipo tiene cupo reservado o vendido.", ErrorType.Conflict));
        if (await tipos.DeactivateAsync(request.EventoId, request.Id, token))
            await audit.WriteAsync(actual.UsuarioId, "DELETE", "tipos_entrada", tipo.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
