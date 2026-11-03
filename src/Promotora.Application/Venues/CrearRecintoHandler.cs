using System.Globalization;
using Promotora.Application.Audit;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Shared;
using Promotora.Domain.Venues;

namespace Promotora.Application.Venues;

public sealed class CrearRecintoHandler(IRecintoRepository recintos, IVenuesTransaction transaction,
    VenuesAccess access, IUsuarioActual actual, IAuditLogger audit, IClock clock) : ICommandHandler<CrearRecintoCommand, RecintoDto>
{
    public Task<Result<RecintoDto>> HandleAsync(CrearRecintoCommand request, CancellationToken ct) => transaction.ExecuteAsync(async token =>
    {
        var actor = await access.ActorAsync(true, token);
        if (actor is null)
        {
            return Result<RecintoDto>.Failure(VenuesAccess.Forbidden);
        }

        int? duenoId = request.DuenoId ?? actor.Value.DuenoId;
        if (duenoId is null || !VenuesAccess.Permite(actor, duenoId.Value))
        {
            return Result<RecintoDto>.Failure(VenuesAccess.Forbidden);
        }

        if (!await recintos.DuenoActivoAsync(duenoId.Value, token))
        {
            return Result<RecintoDto>.Failure(new Error("venues.dueno", "Se requiere un dueño activo.", ErrorType.BusinessRule));
        }

        var recinto = await recintos.InsertAsync(new Recinto(0, duenoId.Value, Normalizar(request.Datos), true, clock.UtcNow), token);
        await audit.WriteAsync(actual.UsuarioId, "CREATE", "recintos", recinto.Id.ToString(CultureInfo.InvariantCulture), actual.IpOrigen, token);
        return Result<RecintoDto>.Success(RecintoDto.From(recinto));
    }, ct);

    internal static DatosRecinto Normalizar(DatosRecinto datos) => datos with
    {
        Nombre = datos.Nombre.Trim(),
        Direccion = datos.Direccion?.Trim(),
        Ciudad = datos.Ciudad?.Trim(),
        ImagenPlanoUrl = datos.ImagenPlanoUrl?.Trim()
    };
}
