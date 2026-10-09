using Promotora.Application.Identity.Registration;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Ports;

public interface IUsuarioRepository
{
    Task<Usuario?> FindByIdAsync(int id, CancellationToken ct);
    Task<Usuario?> FindByEmailAsync(string email, CancellationToken ct);
    Task<Result<UsuarioDto>> CreateAsync(Usuario usuario, PerfilNuevo perfil, int? actorId, string? ip, bool bootstrap, CancellationToken ct);
}
