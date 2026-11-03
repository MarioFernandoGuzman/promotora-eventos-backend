namespace Promotora.WebApi.Identity;

public sealed record RegistroRequest(string Email, string Password, string Nombre, string Apellido,
    DateOnly? FechaNacimiento = null, string? Telefono = null)
{
    public override string ToString() => nameof(RegistroRequest);
}
