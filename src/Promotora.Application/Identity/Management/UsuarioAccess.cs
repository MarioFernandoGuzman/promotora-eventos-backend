using Promotora.Application.Identity.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed class UsuarioAccess(IUsuarioRepository usuarios, IUsuarioActual actual, ISesionRepository sesiones)
{
    public static Error Forbidden => new("identity.forbidden", "No tienes permiso sobre este usuario.", ErrorType.Forbidden);
    public static Error NotFound => new("identity.not_found", "Usuario no encontrado.", ErrorType.NotFound);
    public static Error Conflict => new("identity.conflict", "El usuario cambió de estado.", ErrorType.Conflict);

    public Task<Usuario?> ActorAsync(CancellationToken ct) => actual.UsuarioId is int id ? usuarios.FindByIdAsync(id, ct) : Task.FromResult<Usuario?>(null);

    public async Task LockAsync(int targetId, CancellationToken ct)
    {
        foreach (int id in new[] { actual.UsuarioId ?? 0, targetId }.Where(x => x > 0).Distinct().Order())
        {
            await sesiones.LockUsuarioAsync(id, ct);
        }
    }
}
