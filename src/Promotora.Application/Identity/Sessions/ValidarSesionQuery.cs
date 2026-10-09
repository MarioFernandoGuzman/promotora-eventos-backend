using Promotora.Application.Shared.Handlers;

namespace Promotora.Application.Identity.Sessions;

public sealed record ValidarSesionQuery(int UsuarioId, long SesionId, string Rol, bool DebeCambiarPassword) : IQuery<bool>;
