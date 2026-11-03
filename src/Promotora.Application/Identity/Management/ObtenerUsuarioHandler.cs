using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed class ObtenerUsuarioHandler(IUsuarioRepository usuarios, UsuarioAccess access) : IQueryHandler<ObtenerUsuarioQuery, UsuarioDetalleDto>
{
    public async Task<Result<UsuarioDetalleDto>> HandleAsync(ObtenerUsuarioQuery request, CancellationToken ct)
    {
        var actor = await access.ActorAsync(ct);
        if (actor is null || !actor.PuedeGestionarUsuario(request.Id))
        {
            return Result<UsuarioDetalleDto>.Failure(UsuarioAccess.Forbidden);
        }

        var result = await usuarios.GetDetailAsync(request.Id, ct);
        return result is null ? Result<UsuarioDetalleDto>.Failure(UsuarioAccess.NotFound) : Result<UsuarioDetalleDto>.Success(result);
    }
}
