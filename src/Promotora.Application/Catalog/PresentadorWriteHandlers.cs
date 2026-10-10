using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Catalog;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class CrearPresentadorHandler(IPresentadorRepository repo, ICatalogTransaction transaction, CatalogAccess access,
    IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<CrearPresentadorCommand, PresentadorDto>
{
    public Task<Result<PresentadorDto>> HandleAsync(CrearPresentadorCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (!await access.AdminAsync(token)) return Result<PresentadorDto>.Failure(CatalogAccess.Forbidden);
        var entity = await repo.InsertAsync(new Presentador(0, Normalizar(request.Datos), true), token);
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "presentadores", entity.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<PresentadorDto>.Success(PresentadorDto.From(entity));
    }, ct);

    internal static DatosPresentador Normalizar(DatosPresentador datos) => datos with { Nombre = datos.Nombre.Trim(), Tipo = datos.Tipo.Trim(), Biografia = datos.Biografia?.Trim(), ImagenUrl = datos.ImagenUrl?.Trim() };
}

public sealed class ActualizarPresentadorHandler(IPresentadorRepository repo, ICatalogTransaction transaction, CatalogAccess access,
    IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<ActualizarPresentadorCommand, PresentadorDto>
{
    public Task<Result<PresentadorDto>> HandleAsync(ActualizarPresentadorCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (!await access.AdminAsync(token)) return Result<PresentadorDto>.Failure(CatalogAccess.Forbidden);
        var entity = await repo.FindAsync(request.Id, true, token);
        if (entity is null) return Result<PresentadorDto>.Failure(CatalogAccess.NotFound);
        var edited = entity.Editar(CrearPresentadorHandler.Normalizar(request.Datos));
        if (edited.IsFailure) return Result<PresentadorDto>.Failure(edited.Errors);
        await repo.UpdateAsync(entity, token);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "presentadores", entity.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<PresentadorDto>.Success(PresentadorDto.From(entity));
    }, ct);
}

public sealed class BajaPresentadorHandler(IPresentadorRepository repo, ICatalogTransaction transaction, CatalogAccess access,
    IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<BajaPresentadorCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(BajaPresentadorCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        if (!await access.AdminAsync(token)) return Result<Unit>.Failure(CatalogAccess.Forbidden);
        if (await repo.FindAsync(request.Id, true, token) is null) return Result<Unit>.Failure(CatalogAccess.NotFound);
        if (await repo.DeactivateAsync(request.Id, token))
            await audit.WriteAsync(actual.UsuarioId, "DELETE", "presentadores", request.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}
