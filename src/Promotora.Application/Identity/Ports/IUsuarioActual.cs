namespace Promotora.Application.Identity.Ports;

public interface IUsuarioActual
{
    int? UsuarioId { get; }
    string? IpOrigen { get; }
}
