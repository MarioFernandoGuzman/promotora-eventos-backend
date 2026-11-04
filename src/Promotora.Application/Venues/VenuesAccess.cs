using Promotora.Application.Identity.Ports;
using Promotora.Application.Venues.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Venues;

public sealed class VenuesAccess(IUsuarioActual actual, IUsuarioRepository usuarios, ISesionRepository sesiones, IRecintoRepository recintos)
{
    public static readonly Error Forbidden = new("venues.forbidden", "No tienes permiso para gestionar este recinto.", ErrorType.Forbidden);
    public static readonly Error NotFound = new("venues.not_found", "No se encontró el recinto.", ErrorType.NotFound);

    public Task<(bool Admin, int? DuenoId)?> ActorAsync(bool bloquear, CancellationToken ct) =>
        ResolverActorAsync(bloquear, permitirValidador: false, ct);

    public Task<(bool Admin, int? DuenoId)?> ActorValidacionAsync(CancellationToken ct) =>
        ResolverActorAsync(bloquear: true, permitirValidador: true, ct);

    private async Task<(bool Admin, int? DuenoId)?> ResolverActorAsync(bool bloquear, bool permitirValidador, CancellationToken ct)
    {
        if (actual.UsuarioId is not int id)
        {
            return null;
        }

        if (bloquear)
        {
            await sesiones.LockUsuarioAsync(id, ct);
        }

        var user = await usuarios.FindByIdAsync(id, ct);
        if (user is null || !user.PuedeAutenticarse || user.DebeCambiarPassword)
        {
            return null;
        }

        if (user.EsAdministradorHabilitado || (permitirValidador && user.Rol == RolUsuario.VALIDADOR_BOLETOS))
        {
            return (true, null);
        }

        if (user.Rol == RolUsuario.DUENO_RECINTO && await recintos.DuenoIdAsync(id, ct) is int dueno)
        {
            return (false, dueno);
        }

        return null;
    }

    public static bool Permite((bool Admin, int? DuenoId)? actor, int duenoId) => actor is not null
        && (actor.Value.Admin || actor.Value.DuenoId == duenoId);
}
