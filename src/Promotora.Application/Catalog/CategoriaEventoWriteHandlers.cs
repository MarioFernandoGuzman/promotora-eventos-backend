using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class CrearCategoriaEventoHandler(ICategoriaEventoRepository repo, ICatalogTransaction transaction, CatalogAccess access,
    IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<CrearCategoriaEventoCommand, CategoriaEventoDto>
{
    public Task<Result<CategoriaEventoDto>> HandleAsync(CrearCategoriaEventoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (!await access.AdminAsync(token)) return Result<CategoriaEventoDto>.Failure(CatalogAccess.Forbidden);
        var entity = await repo.InsertAsync(new CategoriaEvento(0, Normalizar(request.Datos), true), token);
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "categorias_evento", entity.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<CategoriaEventoDto>.Success(CategoriaEventoDto.From(entity));
    }, ct);

    internal static DatosCategoriaEvento Normalizar(DatosCategoriaEvento datos) => datos with { Nombre = datos.Nombre.Trim(), Descripcion = datos.Descripcion?.Trim() };
}

public sealed class ActualizarCategoriaEventoHandler(ICategoriaEventoRepository repo, ICatalogTransaction transaction, CatalogAccess access,
    IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<ActualizarCategoriaEventoCommand, CategoriaEventoDto>
{
    public Task<Result<CategoriaEventoDto>> HandleAsync(ActualizarCategoriaEventoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (!await access.AdminAsync(token)) return Result<CategoriaEventoDto>.Failure(CatalogAccess.Forbidden);
        var entity = await repo.FindAsync(request.Id, true, token);
        if (entity is null) return Result<CategoriaEventoDto>.Failure(CatalogAccess.NotFound);
        var edited = entity.Editar(CrearCategoriaEventoHandler.Normalizar(request.Datos));
        if (edited.IsFailure) return Result<CategoriaEventoDto>.Failure(edited.Errors);
        await repo.UpdateAsync(entity, token);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "categorias_evento", entity.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<CategoriaEventoDto>.Success(CategoriaEventoDto.From(entity));
    }, ct);
}

public sealed class BajaCategoriaEventoHandler(ICategoriaEventoRepository repo, ICatalogTransaction transaction, CatalogAccess access,
    IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<BajaCategoriaEventoCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(BajaCategoriaEventoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (!await access.AdminAsync(token)) return Result<Unit>.Failure(CatalogAccess.Forbidden);
        if (await repo.FindAsync(request.Id, true, token) is null) return Result<Unit>.Failure(CatalogAccess.NotFound);
        if (await repo.DeactivateAsync(request.Id, token))
            await audit.WriteAsync(actual.UsuarioId, "DELETE", "categorias_evento", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
