using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Identity.Management;

public sealed record ObtenerUsuarioQuery(int Id) : IQuery<UsuarioDetalleDto>;
