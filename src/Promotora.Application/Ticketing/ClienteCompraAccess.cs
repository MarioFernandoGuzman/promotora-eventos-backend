using Promotora.Application.Identity.Ports;
using Promotora.Application.Ticketing.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Ticketing;

public sealed class ClienteCompraAccess(IUsuarioActual actual, IUsuarioRepository usuarios, ISesionRepository sesiones, ICompraRepository compras)
{
    public static readonly Error Forbidden = new("compra.prohibida", "Se requiere un cliente activo.", ErrorType.Forbidden);
    public static readonly Error NotFound = new("compra.no_encontrada", "Compra no encontrada.", ErrorType.NotFound);

    public async Task<int?> ClienteAsync(bool bloquear, CancellationToken ct)
    {
        if (actual.UsuarioId is not int id) return null;
        if (bloquear) await sesiones.LockUsuarioAsync(id, ct);
        var actor = await usuarios.FindByIdAsync(id, ct);
        if (actor is null || actor.Rol != RolUsuario.CLIENTE || !actor.PuedeAutenticarse || actor.DebeCambiarPassword) return null;
        return await compras.ClienteIdAsync(id, ct);
    }
}
