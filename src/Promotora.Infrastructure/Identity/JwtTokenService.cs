using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Promotora.Application.Identity.Ports;
using Promotora.Application.Shared.Ports;
using Promotora.Domain.Identity;

namespace Promotora.Infrastructure.Identity;

public sealed class JwtTokenService(byte[] key, string issuer, string audience, IClock clock) : ITokenService
{
    public string CreateRefreshToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public string CreateAccessToken(Usuario usuario, long sesionId, DateTime expiraEn)
    {
        var claims = new[]
        {
            new Claim("sub", usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim("role", usuario.Rol.ToString()),
            new Claim("sid", sesionId.ToString(CultureInfo.InvariantCulture)),
            new Claim("password_change_required", usuario.DebeCambiarPassword ? "true" : "false"),
            new Claim("jti", Guid.NewGuid().ToString("N"))
        };
        var token = new JwtSecurityToken(issuer, audience, claims, clock.UtcNow, expiraEn,
            new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
