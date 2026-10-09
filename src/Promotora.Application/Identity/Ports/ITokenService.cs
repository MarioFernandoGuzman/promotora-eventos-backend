using Promotora.Domain.Identity;

namespace Promotora.Application.Identity.Ports;

public interface ITokenService
{
    string CreateRefreshToken();
    string HashRefreshToken(string token);
    string CreateAccessToken(Usuario usuario, long sesionId, DateTime expiraEn);
}
