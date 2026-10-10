using Promotora.Application.Identity.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Catalog;

public sealed class CatalogAccess(IUsuarioActual actual, IUsuarioRepository usuarios, ISesionRepository sesiones)
{
    public static readonly Error Forbidden = new("catalog.forbidden", "No tienes permiso para esta operación del catálogo.", ErrorType.Forbidden);
    public static readonly Error NotFound = new("catalog.not_found", "No se encontró el recurso del catálogo.", ErrorType.NotFound);
    public static readonly Error Inactivo = new("catalog.inactivo", "El recurso está inactivo.", ErrorType.Conflict);

    public async Task<Usuario?> ActorAsync(bool bloquear, CancellationToken ct)
    {
        if (actual.UsuarioId is not int id) return null;
        if (bloquear) await sesiones.LockUsuarioAsync(id, ct);
        var actor = await usuarios.FindByIdAsync(id, ct);
        return actor is { PuedeAutenticarse: true, DebeCambiarPassword: false } ? actor : null;
    }

    public async Task<bool> AdminAsync(CancellationToken ct) => (await ActorAsync(true, ct))?.EsAdministradorHabilitado == true;
}
