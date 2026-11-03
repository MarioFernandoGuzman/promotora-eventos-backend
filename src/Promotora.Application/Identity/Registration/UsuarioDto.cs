namespace Promotora.Application.Identity.Registration;

public sealed record UsuarioDto(int Id, string Email, string Nombre, string Apellido, string Rol, string Estado, bool DebeCambiarPassword);
