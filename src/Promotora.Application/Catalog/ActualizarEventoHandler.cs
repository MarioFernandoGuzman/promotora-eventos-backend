using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class ActualizarEventoHandler(IEventoRepository eventos, ICategoriaEventoRepository categorias, ICatalogTransaction transaction,
    EventoAccess access, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<ActualizarEventoCommand, EventoDto>
{
    public Task<Result<EventoDto>> HandleAsync(ActualizarEventoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var result = await access.FindAsync(request.Id, true, true, token);
        if (result.IsFailure) return Result<EventoDto>.Failure(result.Errors);
        if (await eventos.TieneComprasAsync(request.Id, token)) return Result<EventoDto>.Failure(new Error("catalog.compras", "No se modifica un evento con historial de compras.", ErrorType.Conflict));
        var category = await categorias.FindAsync(request.Datos.CategoriaId, true, token);
        if (category is null) return Result<EventoDto>.Failure(CatalogAccess.NotFound);
        if (!category.Activo) return Result<EventoDto>.Failure(CatalogAccess.Inactivo);
        var edit = result.Value.Editar(CrearEventoHandler.Normalizar(request.Datos));
        if (edit.IsFailure) return Result<EventoDto>.Failure(edit.Errors);
        await eventos.UpdateAsync(result.Value, token);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "eventos", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<EventoDto>.Success(EventoDto.From(result.Value));
    }, ct);
}
