using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Sessions;

public sealed class ValidarSesionHandler(IUsuarioRepository usuarios, ISesionRepository sesiones) : IQueryHandler<ValidarSesionQuery, bool>
{
    public async Task<Result<bool>> HandleAsync(ValidarSesionQuery request, CancellationToken ct)
    {
        var usuario = await usuarios.FindByIdAsync(request.UsuarioId, ct);
        var active = usuario is { PuedeAutenticarse: true } && usuario.Rol.ToString() == request.Rol
            && usuario.DebeCambiarPassword == request.DebeCambiarPassword
            && await sesiones.IsActiveAsync(request.SesionId, usuario.Id, ct);
        return Result<bool>.Success(active);
    }
}
