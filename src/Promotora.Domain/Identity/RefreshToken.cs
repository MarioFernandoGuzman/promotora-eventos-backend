namespace Promotora.Domain.Identity;

public sealed record RefreshToken(long Id, int UsuarioId, Guid FamiliaId, DateTime ExpiraEn, DateTime? RevocadoEn)
{
    public bool PuedeRotar(DateTime utcNow) => RevocadoEn is null && ExpiraEn > utcNow;
}
