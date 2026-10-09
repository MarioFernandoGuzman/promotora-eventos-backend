using Promotora.Application.Identity.Management;
using Promotora.Application.Identity.Registration;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Ports;

public interface IUsuarioRepository
{
    Task<Usuario?> FindByIdAsync(int id, CancellationToken ct);
    Task<Usuario?> FindByEmailAsync(string email, CancellationToken ct);
    Task<Result<UsuarioDto>> CreateAsync(Usuario usuario, PerfilNuevo perfil, int? actorId, string? ip, bool bootstrap, CancellationToken ct);
    Task<UsuarioDetalleDto?> GetDetailAsync(int id, CancellationToken ct);
    Task<UsuariosPaginaDto> ListAsync(int page, int pageSize, CancellationToken ct);
    Task UpdateAsync(int id, string email, string nombre, string apellido, PerfilNuevo perfil, CancellationToken ct);
    Task<bool> DeactivateAsync(int id, string expectedState, CancellationToken ct);
    Task ChangePasswordAsync(int id, string hash, CancellationToken ct);
}
