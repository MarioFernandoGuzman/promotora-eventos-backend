namespace Promotora.WebApi.Identity;

public sealed record AltaValidadorRequest(string Email, string Password, string Nombre, string Apellido)
{
    public override string ToString() => nameof(AltaValidadorRequest);
}
