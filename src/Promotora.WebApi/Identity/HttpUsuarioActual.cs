using System.Security.Claims;
using Promotora.Application.Identity.Ports;

namespace Promotora.WebApi.Identity;

public sealed class HttpUsuarioActual(IHttpContextAccessor accessor) : IUsuarioActual
{
    public int? UsuarioId => accessor.HttpContext?.User.Identity?.IsAuthenticated == true
        && int.TryParse(accessor.HttpContext.User.FindFirstValue("sub"), out int id) ? id : null;
    public string? IpOrigen => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
