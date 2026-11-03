using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Registration;

public sealed class BootstrapAdminHandler(IUsuarioRepository repository, IPasswordService passwords, IClock clock)
    : ICommandHandler<BootstrapAdminCommand, UsuarioDto>
{
    public Task<Result<UsuarioDto>> HandleAsync(BootstrapAdminCommand request, CancellationToken ct)
    {
        var usuario = new Usuario(0, request.Email.Trim().ToLowerInvariant(), passwords.Hash(request.Password),
            "Administrador", "Inicial", RolUsuario.ADMINISTRADOR, "ACTIVO", true, clock.UtcNow);
        return repository.CreateAsync(usuario, new PerfilNuevo(NivelAcceso: "SUPERADMIN"), null, null, true, ct);
    }
}
