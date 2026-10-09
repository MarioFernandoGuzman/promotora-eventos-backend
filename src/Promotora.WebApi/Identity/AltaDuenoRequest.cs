namespace Promotora.WebApi.Identity;

public sealed record AltaDuenoRequest(string Email, string Password, string Nombre, string Apellido, string Empresa, string? Nit = null)
{
    public override string ToString() => nameof(AltaDuenoRequest);
}
