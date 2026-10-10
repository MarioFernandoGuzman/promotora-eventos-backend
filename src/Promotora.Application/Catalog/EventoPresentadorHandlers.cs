using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Catalog.Ports;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class AsociarPresentadorHandler(IEventoPresentadorRepository relaciones, IPresentadorRepository presentadores,
    EventoConfigurationAccess access, ICatalogTransaction transaction, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<AsociarPresentadorCommand, EventoPresentadorDto>
{
    public Task<Result<EventoPresentadorDto>> HandleAsync(AsociarPresentadorCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var result = await access.FindAsync(request.EventoId, token);
        if (result.IsFailure) return Result<EventoPresentadorDto>.Failure(result.Errors);
        var presenter = await presentadores.FindAsync(request.PresentadorId, true, token);
        if (presenter is null) return Result<EventoPresentadorDto>.Failure(CatalogAccess.NotFound);
        if (!presenter.Activo) return Result<EventoPresentadorDto>.Failure(CatalogAccess.Inactivo);
        string? rol = request.Rol?.Trim();
        await relaciones.InsertAsync(request.EventoId, request.PresentadorId, rol, token);
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "evento_presentadores", Key(request.EventoId, request.PresentadorId), actual.IpOrigen, token);
        return Result<EventoPresentadorDto>.Success(new(request.EventoId, presenter.Id, presenter.Datos.Nombre, presenter.Datos.Tipo, rol, presenter.Activo));
    }, ct);

    internal static string Key(int evento, int presentador) => string.Create(CultureInfo.InvariantCulture, $"{evento}:{presentador}");
}

public sealed class EditarRolPresentadorHandler(IEventoPresentadorRepository relaciones, IPresentadorRepository presentadores,
    EventoConfigurationAccess access, ICatalogTransaction transaction, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<EditarRolPresentadorCommand, EventoPresentadorDto>
{
    public Task<Result<EventoPresentadorDto>> HandleAsync(EditarRolPresentadorCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var result = await access.FindAsync(request.EventoId, token);
        if (result.IsFailure) return Result<EventoPresentadorDto>.Failure(result.Errors);
        var relation = await relaciones.FindAsync(request.EventoId, request.PresentadorId, token);
        if (relation is null) return Result<EventoPresentadorDto>.Failure(CatalogAccess.NotFound);
        var presenter = await presentadores.FindAsync(request.PresentadorId, true, token);
        if (presenter is null || !presenter.Activo) return Result<EventoPresentadorDto>.Failure(CatalogAccess.Inactivo);
        string? rol = request.Rol?.Trim();
        await relaciones.UpdateRoleAsync(request.EventoId, request.PresentadorId, rol, token);
        await audit.WriteAsync(actual.UsuarioId, "UPDATE", "evento_presentadores", AsociarPresentadorHandler.Key(request.EventoId, request.PresentadorId), actual.IpOrigen, token);
        return Result<EventoPresentadorDto>.Success(relation with { Rol = rol, Nombre = presenter.Datos.Nombre, Tipo = presenter.Datos.Tipo, Activo = presenter.Activo });
    }, ct);
}

public sealed class RetirarPresentadorHandler(IEventoPresentadorRepository relaciones, EventoConfigurationAccess access,
    ICatalogTransaction transaction, IUsuarioActual actual, IAuditLogger audit) : ICommandHandler<RetirarPresentadorCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(RetirarPresentadorCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var result = await access.FindAsync(request.EventoId, token);
        if (result.IsFailure) return Result<Unit>.Failure(result.Errors);
        if (await relaciones.DeleteAsync(request.EventoId, request.PresentadorId, token))
            await audit.WriteAsync(actual.UsuarioId, "DELETE", "evento_presentadores", AsociarPresentadorHandler.Key(request.EventoId, request.PresentadorId), actual.IpOrigen, token);
        return Result<Unit>.Success(Unit.Value);
    }, ct);
}

public sealed class ListarEventoPresentadoresHandler(IEventoPresentadorRepository relaciones, EventoAccess access) : IQueryHandler<ListarEventoPresentadoresQuery, PaginaDto<EventoPresentadorDto>>
{
    public async Task<Result<PaginaDto<EventoPresentadorDto>>> HandleAsync(ListarEventoPresentadoresQuery request, CancellationToken ct)
    {
        var result = await access.FindAsync(request.EventoId, false, false, ct);
        return result.IsFailure ? Result<PaginaDto<EventoPresentadorDto>>.Failure(result.Errors)
            : Result<PaginaDto<EventoPresentadorDto>>.Success(await relaciones.ListAsync(request.EventoId, request.Page, request.PageSize, ct));
    }
}
