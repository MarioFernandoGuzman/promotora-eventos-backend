namespace Promotora.Domain.Identity;

public sealed class Usuario
{
    public int Id { get; }
    public string Email { get; }
    public string PasswordHash { get; }
    public string Nombre { get; }
    public string Apellido { get; }
    public RolUsuario Rol { get; }
    public string Estado { get; }
    public bool DebeCambiarPassword { get; }
    public DateTime FechaRegistro { get; }

    public Usuario(int id, string email, string passwordHash, string nombre, string apellido,
        RolUsuario rol, string estado, bool debeCambiarPassword, DateTime fechaRegistro)
    {
        if (!Enum.IsDefined(rol) || estado is not ("ACTIVO" or "INACTIVO" or "SUSPENDIDO"))
        {
            throw new ArgumentException("Rol o estado inválido.");
        }

        if (fechaRegistro.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha debe ser UTC.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(apellido);
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        Nombre = nombre;
        Apellido = apellido;
        Rol = rol;
        Estado = estado;
        DebeCambiarPassword = debeCambiarPassword;
        FechaRegistro = fechaRegistro;
    }

    public bool PuedeAutenticarse => Estado == "ACTIVO";
    public bool EsAdministradorHabilitado => PuedeAutenticarse && !DebeCambiarPassword && Rol == RolUsuario.ADMINISTRADOR;
    public bool PuedeGestionarUsuario(int usuarioId) => PuedeAutenticarse && !DebeCambiarPassword && (Id == usuarioId || Rol == RolUsuario.ADMINISTRADOR);
    public bool PuedeCambiarPassword(int usuarioId) => PuedeAutenticarse && Id == usuarioId;
}
