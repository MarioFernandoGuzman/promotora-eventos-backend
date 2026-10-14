using Promotora.Application.Identity.Ports;
using Promotora.Domain.Identity;

namespace Promotora.Application.AI.Documentos;

public sealed class DocumentoAccess(IUsuarioActual actual, IUsuarioRepository usuarios, ISesionRepository sesiones)
{
    public async Task<bool> AdministradorAsync(CancellationToken ct)
    {
        if (actual.UsuarioId is not int id) return false;
        await sesiones.LockUsuarioAsync(id, ct);
        var usuario = await usuarios.FindByIdAsync(id, ct);
        return usuario is { PuedeAutenticarse: true, DebeCambiarPassword: false, Rol: RolUsuario.ADMINISTRADOR };
    }
}
