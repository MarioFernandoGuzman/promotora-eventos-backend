using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Identity.Management;

public sealed record ListarUsuariosQuery(int Page = 1, int PageSize = 20) : IQuery<UsuariosPaginaDto>;
