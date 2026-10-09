namespace Promotora.WebApi.Identity;

public sealed record CambiarPasswordRequest(string PasswordActual, string PasswordNuevo)
{
    public override string ToString() => nameof(CambiarPasswordRequest);
}
