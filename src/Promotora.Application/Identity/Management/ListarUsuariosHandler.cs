using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Handlers;
using Promotora.Domain.Shared;

namespace Promotora.Application.Identity.Management;

public sealed class ListarUsuariosHandler(IUsuarioRepository usuarios, UsuarioAccess access) : IQueryHandler<ListarUsuariosQuery, UsuariosPaginaDto>
{
    public async Task<Result<UsuariosPaginaDto>> HandleAsync(ListarUsuariosQuery request, CancellationToken ct)
    {
        if (await access.ActorAsync(ct) is not { EsAdministradorHabilitado: true })
        {
            return Result<UsuariosPaginaDto>.Failure(UsuarioAccess.Forbidden);
        }

        return Result<UsuariosPaginaDto>.Success(await usuarios.ListAsync(request.Page, request.PageSize, ct));
    }
}
