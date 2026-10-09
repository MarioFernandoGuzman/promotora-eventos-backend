namespace Promotora.Application.Identity.Sessions;

public sealed record TokenDto(string AccessToken, string? RefreshToken, DateTime ExpiraEn, bool DebeCambiarPassword, int UsuarioId)
{
    public override string ToString() => nameof(TokenDto);
}
