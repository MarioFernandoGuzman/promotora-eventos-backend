namespace Promotora.WebApi.Identity;

public sealed record AltaAdministradorRequest(string Email, string Password, string Nombre, string Apellido)
{
    public override string ToString() => nameof(AltaAdministradorRequest);
}
