namespace Promotora.WebApi.Identity;

public sealed record ActualizarUsuarioRequest(string Email, string Nombre, string Apellido,
    DateOnly? FechaNacimiento = null, string? Telefono = null, string? Empresa = null, string? Nit = null);
